using System.Collections.ObjectModel;
using System.Drawing;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Editor.Completion;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    private IReadOnlyList<CompletionItem> _completionItems = [];
    private ListView? _completionListView;
    private Popover<ListView, CompletionItem?>? _completionPopover;
    private int _completionPrefixStart;
    private int _completionSelectedIndex;

    /// <summary>
    ///     Gets or sets the completion provider that supplies suggestions for the in-editor
    ///     autocomplete popup. Set to <see langword="null" /> to disable completion.
    /// </summary>
    public IEditorCompletionProvider? CompletionProvider
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;

            if (value is null)
            {
                DismissCompletion ();
            }
        }
    }

    /// <summary>Whether the completion session is currently active (items are available).</summary>
    public bool IsCompletionActive => _completionItems.Count > 0;

    /// <summary>Gets the zero-based index of the currently selected completion item.</summary>
    internal int CompletionSelectedIndex => _completionSelectedIndex;

    /// <summary>
    ///     Extracts the word-prefix immediately before the caret (letters, digits, underscores)
    ///     for use as the completion filter string. Returns empty when the caret follows
    ///     whitespace or punctuation.
    /// </summary>
    internal string GetCompletionPrefix ()
    {
        return GetCompletionPrefix (out _);
    }

    /// <summary>
    ///     Extracts the word-prefix immediately before the caret and also returns the document
    ///     offset where the prefix starts.
    /// </summary>
    internal string GetCompletionPrefix (out int prefixStart)
    {
        if (_document is null)
        {
            prefixStart = 0;

            return string.Empty;
        }

        var offset = CaretOffset;
        var start = offset;

        while (start > 0)
        {
            var ch = _document.GetCharAt (start - 1);

            if (!char.IsLetterOrDigit (ch) && ch != '_')
            {
                break;
            }

            start--;
        }

        prefixStart = start;

        return start < offset ? _document.GetText (start, offset - start) : string.Empty;
    }

    /// <summary>
    ///     Called before normal key dispatch. Returns <see langword="true" /> when the completion
    ///     popup consumed the key (navigation / accept / dismiss). This runs
    ///     in <see cref="OnKeyDown" />, which fires before command bindings.
    /// </summary>
    internal bool HandleCompletionKey (Key key)
    {
        if (CompletionProvider is null)
        {
            return false;
        }

        // An active popup gets first crack at navigation keys.
        if (IsCompletionActive)
        {
            // Esc (Command.Quit) or a horizontal caret move (Left/Right) dismisses the popup.
            // Up/Down are intentionally absent: the focused popup ListView consumes them to
            // move the selection, so they never reach here.
            if (KeyMatches (Command.Quit) || KeyMatches (Command.Left) || KeyMatches (Command.Right))
            {
                DismissCompletion ();

                return false;
            }

            // Accept on the keys bound to NewLine (Enter) / InsertTab (Tab). SPACE is
            // deliberately NOT an accept key: it falls through, inserts a space, and the
            // now-empty prefix dismisses the popup (so "this is a test." stays intact).
            if (KeyMatches (Command.NewLine) || KeyMatches (Command.InsertTab))
            {
                AcceptCompletion ();

                return true;
            }

            // Character keys: insert directly into the document while the popup is open,
            // then refresh the completion list. 
            if (key is { IsCtrl: false, IsAlt: false, AsRune: { } rune } && !Rune.IsControl (rune))
            {
                if (_document is null || ReadOnly)
                {
                    return true;
                }

                if (HasSelection)
                {
                    ReplaceSelection (rune.ToString ());
                }
                else if (OverwriteMode)
                {
                    OverwriteAtCaret (rune.ToString ());
                }
                else
                {
                    _document.Insert (CaretOffset, rune.ToString ());
                }

                // Refresh the completion list with the updated prefix.
                NotifyCompletionAfterInsert ();

                return true;
            }

            // Backspace: delete the character before the caret and refresh.
            if (key == Key.Backspace)
            {
                if (_document is not null && !ReadOnly && CaretOffset > 0)
                {
                    _document.Remove (CaretOffset - 1, 1);
                }

                NotifyCompletionAfterInsert ();

                return true;
            }
        }

        // Check provider-specific triggers (e.g. Ctrl+Space).
        if (!CompletionProvider.ShouldTrigger (key))
        {
            return false;
        }

        ShowCompletion ();

        return true;

        // Resolve a key against the Editor's own bindings instead of hardcoding literals,
        // so completion follows any rebinding of these commands.
        bool KeyMatches (Command command)
        {
            return KeyBindings.GetFirstFromCommands (command) is { } bound && key == bound;
        }
    }

    /// <summary>
    ///     Handles mouse events for the completion popup: a single click inside the popup
    ///     area selects and accepts the clicked item; a click outside dismisses the popup.
    ///     The hit-test maps screen Y to an item index and does not account for a scrolled
    ///     list (only the first <see cref="ShowCompletionPopup" /> page is addressable).
    /// </summary>
    internal bool HandleCompletionMouse (Mouse mouse)
    {
        if (!IsCompletionActive || _completionPopover is null || _completionListView is null)
        {
            return false;
        }

        if (!mouse.IsSingleClicked)
        {
            return false;
        }

        // Map the click's screen position to the Popover's content area.
        // The ListView's frame within the Popover determines the hit region.
        Rectangle popoverScreenFrame = _completionPopover.Frame;

        if (mouse.ScreenPosition.X < popoverScreenFrame.X
            || mouse.ScreenPosition.X >= popoverScreenFrame.Right
            || mouse.ScreenPosition.Y < popoverScreenFrame.Y
            || mouse.ScreenPosition.Y >= popoverScreenFrame.Bottom)
        {
            // Click is outside the popup — dismiss.
            DismissCompletion ();

            return false;
        }

        // Determine which item was clicked by Y offset within the Popover.
        var clickedIdx = mouse.ScreenPosition.Y - popoverScreenFrame.Y;

        if (clickedIdx < 0 || clickedIdx >= _completionItems.Count)
        {
            return false;
        }

        _completionSelectedIndex = clickedIdx;
        AcceptCompletion ();

        return true;
    }

    /// <summary>
    ///     Called after a character is inserted into the document. Refreshes or opens the
    ///     completion popup if a provider is active.
    /// </summary>
    internal void NotifyCompletionAfterInsert ()
    {
        if (CompletionProvider is null)
        {
            return;
        }

        var prefix = GetCompletionPrefix (out var prefixStart);

        if (prefix.Length == 0)
        {
            DismissCompletion ();

            return;
        }

        IReadOnlyList<CompletionItem> items =
            CompletionProvider.GetCompletions (_document!, CaretOffset, prefix);

        if (items.Count == 0)
        {
            DismissCompletion ();

            return;
        }

        _completionPrefixStart = prefixStart;
        _completionItems = items;
        _completionSelectedIndex = 0;
        ShowCompletionPopup ();
    }

    /// <summary>Opens the completion popup, querying the provider for items.</summary>
    internal void ShowCompletion ()
    {
        if (CompletionProvider is null || _document is null)
        {
            return;
        }

        var prefix = GetCompletionPrefix (out var prefixStart);

        IReadOnlyList<CompletionItem> items =
            CompletionProvider.GetCompletions (_document, CaretOffset, prefix);

        if (items.Count == 0)
        {
            DismissCompletion ();

            return;
        }

        _completionPrefixStart = prefixStart;
        _completionItems = items;
        _completionSelectedIndex = 0;
        ShowCompletionPopup ();
    }

    /// <summary>Tears down the completion session: disposes the popup and clears the item list.</summary>
    internal void DismissCompletion ()
    {
        DisposeCompletionPopover ();
        _completionItems = [];
    }

    /// <summary>
    ///     Disposes the popover and its ListView (if any) without clearing
    ///     <see cref="_completionItems" /> — used both to dismiss and to swap in a fresh popup.
    /// </summary>
    /// <remarks>
    ///     Null-out-then-dispose order plus unsubscribing first makes this reentrant-safe:
    ///     disposing the popover flips <c>Visible</c>, but the handler is already detached and
    ///     the field already <see langword="null" />, so re-entry is a no-op.
    /// </remarks>
    private void DisposeCompletionPopover ()
    {
        Popover<ListView, CompletionItem?>? popover = _completionPopover;
        _completionPopover = null;
        _completionListView = null;

        if (popover is null)
        {
            return;
        }

        popover.VisibleChanged -= OnCompletionPopoverVisibleChanged;
        popover.Visible = false;
        popover.Dispose ();
    }

    /// <summary>
    ///     Terminal.Gui auto-hides the popover on Esc, a click outside it, focus change, or
    ///     another popover opening — it just flips <c>Visible</c> and never tells the Editor.
    ///     Without this, <see cref="IsCompletionActive" /> would stay <see langword="true" />
    ///     and a subsequent Enter/click would fire a phantom <see cref="AcceptCompletion" />.
    /// </summary>
    private void OnCompletionPopoverVisibleChanged (object? sender, EventArgs e)
    {
        if (_completionPopover is { Visible: false })
        {
            DismissCompletion ();
        }
    }

    /// <summary>
    ///     Accepts the currently selected completion item: replaces the prefix with the
    ///     item's insert text inside a single undo group.
    /// </summary>
    internal void AcceptCompletion ()
    {
        if (!IsCompletionActive || _document is null || _completionItems.Count == 0)
        {
            return;
        }

        if (_completionSelectedIndex < 0 || _completionSelectedIndex >= _completionItems.Count)
        {
            DismissCompletion ();

            return;
        }

        CompletionItem selected = _completionItems[_completionSelectedIndex];
        var insertText = selected.TextToInsert;
        var replaceLength = CaretOffset - _completionPrefixStart;

        DismissCompletion ();

        // Single undo step for the replacement.
        using (_document.RunUpdate ())
        {
            if (replaceLength > 0)
            {
                _document.Replace (_completionPrefixStart, replaceLength, insertText);
            }
            else
            {
                _document.Insert (CaretOffset, insertText);
            }
        }
    }

    private void ShowCompletionPopup ()
    {
        if (_completionItems.Count == 0)
        {
            return;
        }

        // Drop the previous popover (if any) — a fresh one is built each time so the
        // list rebuilds. Reentrant-safe so the VisibleChanged teardown can't NRE here.
        DisposeCompletionPopover ();

        // Build the label list for the ListView.
        ObservableCollection<string> labels = new (_completionItems.Select (i => i.Label));

        // Cap visible height at 10 items to avoid oversized popups.
        var visibleCount = Math.Min (_completionItems.Count, 10);

        _completionListView = new ListView
        {
            Source = new ListWrapper<string> (labels),
            Width = _completionItems.Max (i => i.Label.Length) + 2,
            Height = visibleCount,
            TabStop = TabBehavior.NoStop
        };
        // The ListView owns no accept/dismiss semantics: HandleCompletionKey resolves
        // accept (Enter/Tab) and dismiss (Esc/Left/Right) at the Editor level, while the
        // focused ListView's own Up/Down move the selection. Binding Space->Accept here
        // would silently re-introduce the "this is a test." accept-on-space bug. Disable
        // type-ahead so a stray letter can't hijack the list instead of reaching the editor.
        _completionListView.KeystrokeNavigator = null;
        _completionListView.SelectedItem = _completionSelectedIndex;

        IReadOnlyList<CompletionItem> capturedItems = _completionItems;

        _completionPopover = new Popover<ListView, CompletionItem?> (_completionListView)
        {
            Target = new WeakReference<View> (this),
            ResultExtractor = lv =>
            {
                if (lv.SelectedItem is not { } idx || idx < 0 || idx >= capturedItems.Count)
                {
                    return null;
                }

                return capturedItems[idx];
            },
            TabStop = TabBehavior.NoStop
        };

        // Tear the session down when TG auto-hides the popover (Esc / click-outside /
        // focus change), so IsCompletionActive can't go stale and trigger a phantom accept.
        _completionPopover.VisibleChanged += OnCompletionPopoverVisibleChanged;

        // Position the popup just below the caret.
        Point caretScreen = GetCaretScreenPosition ();
        _completionPopover.MakeVisible (new Point (caretScreen.X, caretScreen.Y + 1));

        // The focused ListView's Up/Down move its selection; mirror that into
        // _completionSelectedIndex so AcceptCompletion inserts the right item.
        // Accept/dismiss keys are resolved separately in HandleCompletionKey.
        _completionListView.ValueChanged += (_, args) =>
        {
            if (args.NewValue is not null)
            {
                _completionSelectedIndex = args.NewValue.Value;
            }
        };
    }

    /// <summary>
    ///     Computes the screen position of the caret (for popup anchoring).
    /// </summary>
    private Point GetCaretScreenPosition ()
    {
        if (_document is null)
        {
            return Point.Empty;
        }

        Rectangle viewport = Viewport;
        var caretLine = GetCaretVisibleLineIndex ();
        var caretCol = GetCaretColumn ();
        var row = caretLine - viewport.Y;
        var col = caretCol - viewport.X;

        return ViewportToScreen (new Point (col, row));
    }
}

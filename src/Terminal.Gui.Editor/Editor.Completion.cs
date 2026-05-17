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
    ///     popup consumed the key (navigation / accept / dismiss / character insert). This runs
    ///     in <see cref="OnKeyDown" />, which fires before command bindings.
    /// </summary>
    /// <remarks>
    ///     The Popover is set to <c>Enabled = false</c> after <see cref="IPopoverView.MakeVisible" />
    ///     so that it functions as a visual-only overlay — the Editor retains keyboard focus and
    ///     all keyboard events flow through this method. This mirrors how
    ///     <c>PopupAutocomplete.ProcessKey</c> in Terminal.Gui works: the host control intercepts
    ///     keys and routes navigation / accept / dismiss / character insertion to the autocomplete
    ///     system. Mouse interaction is handled by <see cref="HandleCompletionMouse" />.
    /// </remarks>
    internal bool HandleCompletionKey (Key key)
    {
        if (CompletionProvider is null)
        {
            return false;
        }

        // An active popup gets first crack at navigation keys.
        if (IsCompletionActive)
        {
            if (key == Key.Esc)
            {
                DismissCompletion ();

                return true;
            }

            if (key == Key.Enter || key == Key.Tab)
            {
                AcceptCompletion ();

                return true;
            }

            if (key == Key.CursorUp)
            {
                var newIdx = (_completionSelectedIndex - 1 + _completionItems.Count) % _completionItems.Count;
                _completionSelectedIndex = newIdx;
                UpdateCompletionListSelection (newIdx);

                return true;
            }

            if (key == Key.CursorDown)
            {
                var newIdx = (_completionSelectedIndex + 1) % _completionItems.Count;
                _completionSelectedIndex = newIdx;
                UpdateCompletionListSelection (newIdx);

                return true;
            }

            // Character keys: insert directly into the document while the popup is open,
            // then refresh the completion list. We handle this here (rather than letting it
            // fall through to OnKeyDownNotHandled) because the Popover is Enabled = false
            // and this is the only place that processes keyboard input during a completion
            // session.
            if (!key.IsCtrl && !key.IsAlt && key.AsRune is { } rune && !Rune.IsControl (rune))
            {
                if (_document is not null && !ReadOnly)
                {
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
                }

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
        if (CompletionProvider.ShouldTrigger (key))
        {
            ShowCompletion ();

            return true;
        }

        return false;
    }

    /// <summary>
    ///     Handles mouse events for the completion popup. When the popup is visible
    ///     (rendered as a <c>Popover</c> with <c>Enabled = false</c>), mouse clicks
    ///     in the popup area are detected by the Editor and mapped to completion items.
    ///     Single-click on an item accepts the completion.
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

    /// <summary>Hides the completion popup if it is visible.</summary>
    internal void DismissCompletion ()
    {
        if (_completionPopover is not null)
        {
            _completionPopover.Visible = false;
            _completionPopover.Dispose ();
            _completionPopover = null;
            _completionListView = null;
        }

        _completionItems = [];
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

    /// <summary>Updates the visible ListView selection if the list view exists.</summary>
    private void UpdateCompletionListSelection (int index)
    {
        if (_completionListView is null)
        {
            return;
        }

        _completionListView.SelectedItem = index;
        _completionListView.SetNeedsDraw ();
        _completionPopover?.SetNeedsDraw ();
    }

    private void ShowCompletionPopup ()
    {
        if (_completionItems.Count == 0)
        {
            return;
        }

        // Dispose previous popover if any — create fresh each time so the list is rebuilt.
        if (_completionPopover is not null)
        {
            _completionPopover.Visible = false;
            _completionPopover.Dispose ();
            _completionPopover = null;
            _completionListView = null;
        }

        // Build the label list for the ListView.
        ObservableCollection<string> labels = new (_completionItems.Select (i => i.Label));

        // Cap visible height at 10 items to avoid oversized popups.
        var visibleCount = Math.Min (_completionItems.Count, 10);

        _completionListView = new ListView
        {
            Source = new ListWrapper<string> (labels),
            Width = _completionItems.Max (i => i.Label.Length) + 2,
            Height = visibleCount
        };
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
            }
        };

        // Position the popup just below the caret.
        Point caretScreen = GetCaretScreenPosition ();
        _completionPopover.MakeVisible (new Point (caretScreen.X, caretScreen.Y + 1));

        // Disable the Popover so it acts as a visual-only overlay. All keyboard events
        // flow to the Editor via HandleCompletionKey (the Editor retains focus). Mouse
        // clicks in the popup area are handled by HandleCompletionMouse called from
        // OnMouseEvent. This follows the PopupAutocomplete pattern from Terminal.Gui:
        // the host control owns all input dispatch, the popup just renders.
        _completionPopover.Enabled = false;
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

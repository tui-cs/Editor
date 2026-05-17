using System.Collections.ObjectModel;
using System.Drawing;
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
    ///     popup consumed the key (navigation / accept / dismiss / trigger keys).
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

    /// <summary>Updates the visible ListView selection when the Popover is showing.</summary>
    private void UpdateCompletionListSelection (int index)
    {
        if (_completionListView is null)
        {
            return;
        }

        _completionListView.SelectedItem = index;
        _completionListView.SetNeedsDraw ();
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

        // Disable keyboard dispatch so the Popover doesn't capture text input.
        // All navigation (Up/Down/Enter/Tab/Esc) is handled by HandleCompletionKey.
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

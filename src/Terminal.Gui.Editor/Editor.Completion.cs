using System.Drawing;
using Terminal.Gui.Editor.Completion;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    private IReadOnlyList<CompletionItem> _completionItems = [];
    private int _completionPrefixStart;
    private PopoverMenu? _completionPopup;
    private int _completionSelectedIndex;

    /// <summary>
    ///     Gets or sets the completion provider that supplies suggestions for the in-editor
    ///     autocomplete popup. Set to <see langword="null" /> to disable completion.
    /// </summary>
    public IEditorCompletionProvider? CompletionProvider { get; set; }

    /// <summary>Whether the completion session is currently active (items are available).</summary>
    public bool IsCompletionActive => _completionItems.Count > 0;

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
                SelectCompletionItem ((_completionSelectedIndex - 1 + _completionItems.Count) % _completionItems.Count);

                return true;
            }

            if (key == Key.CursorDown)
            {
                SelectCompletionItem ((_completionSelectedIndex + 1) % _completionItems.Count);

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
        if (_completionPopup is not null)
        {
            _completionPopup.Visible = false;
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

    private void ShowCompletionPopup ()
    {
        Point caretScreen = GetCaretScreenPosition ();

        // Build menu items from the completion items.
        var menuItems = new MenuItem[_completionItems.Count];

        for (var i = 0; i < _completionItems.Count; i++)
        {
            CompletionItem item = _completionItems[i];
            var label = i == _completionSelectedIndex ? $"> {item.Label}" : $"  {item.Label}";
            menuItems[i] = new MenuItem { Title = label };
        }

        // Dispose previous popup if any — create fresh each time so the Root is rebuilt.
        if (_completionPopup is not null)
        {
            _completionPopup.Visible = false;
        }

        _completionPopup = new PopoverMenu (menuItems)
        {
            Target = new WeakReference<View> (this)
        };

        // Position the popup just below the caret.
        _completionPopup.MakeVisible (new Point (caretScreen.X, caretScreen.Y + 1));
    }

    private void SelectCompletionItem (int index)
    {
        if (_completionItems.Count == 0)
        {
            return;
        }

        _completionSelectedIndex = index;
        ShowCompletionPopup ();
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

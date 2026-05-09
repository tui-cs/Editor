using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

/// <summary>
///     Single-document text editor View backed by <see cref="TextDocument" />. Renders multi-line
///     text from a rope-backed document, tracks a caret offset, dispatches keyboard input to
///     navigate / edit, and scrolls content when it exceeds the viewport.
/// </summary>
public partial class Editor : View
{
    private int _caretOffset;
    private TextDocument? _document;
    private ISyntaxHighlighter? _syntaxHighlighter;
    private string _syntaxLanguage = "csharp";

    /// <summary>
    ///     Sticky column for vertical caret moves. Tracks the column the user *intends* to be in,
    ///     even when the current line is shorter, so Up/Down across short lines snap back to the
    ///     original column on the first long line.
    /// </summary>
    private int _virtualCaretColumn;

    /// <summary>Initializes a new <see cref="Editor" /> with a placeholder document containing "Hello world".</summary>
    public Editor ()
    {
        CanFocus = true;
        CreateCommandsAndBindings ();
        Document = new ("Hello world");
    }

    /// <summary>The backing <see cref="TextDocument" />. Setting this rewires change handlers and clamps the caret.</summary>
    public TextDocument? Document
    {
        get => _document;
        set
        {
            ArgumentNullException.ThrowIfNull (value);

            if (ReferenceEquals (_document, value))
            {
                return;
            }

            if (_document is not null)
            {
                _document.Changed -= OnDocumentChanged;
            }

            _document = value;
            _document.Changed += OnDocumentChanged;

            _caretOffset = Math.Clamp (_caretOffset, 0, _document.TextLength);
            _virtualCaretColumn = GetCaretColumn ();
            UpdateContentSize ();
            SetNeedsDraw ();
        }
    }

    /// <summary>
    ///     Current caret offset. Clamped to <c>[0, Document.TextLength]</c>. Setting this scrolls the
    ///     viewport to keep the caret visible and raises <see cref="CaretChanged" />.
    /// </summary>
    public int CaretOffset
    {
        get => _caretOffset;
        set => SetCaretOffset (value, true);
    }

    /// <summary>Optional syntax highlighter used when drawing document text.</summary>
    public ISyntaxHighlighter? SyntaxHighlighter
    {
        get => _syntaxHighlighter;
        set
        {
            if (ReferenceEquals (_syntaxHighlighter, value))
            {
                return;
            }

            _syntaxHighlighter = value;
            _syntaxHighlighter?.ResetState ();
            SetNeedsDraw ();
        }
    }

    /// <summary>The language identifier passed to <see cref="SyntaxHighlighter" />. Defaults to C#.</summary>
    public string SyntaxLanguage
    {
        get => _syntaxLanguage;
        set
        {
            ArgumentNullException.ThrowIfNull (value);

            if (_syntaxLanguage == value)
            {
                return;
            }

            _syntaxLanguage = value;
            _syntaxHighlighter?.ResetState ();
            SetNeedsDraw ();
        }
    }

    /// <summary>Raised whenever <see cref="Document" /> raises its own <c>Changed</c> event.</summary>
    public event EventHandler<DocumentChangeEventArgs>? DocumentChanged;

    /// <summary>Raised whenever <see cref="CaretOffset" /> changes.</summary>
    public event EventHandler? CaretChanged;

    private void SetCaretOffset (int value, bool resetVirtualColumn)
    {
        var clamped = Math.Clamp (value, 0, _document?.TextLength ?? 0);

        if (clamped == _caretOffset && !resetVirtualColumn)
        {
            return;
        }

        var changed = clamped != _caretOffset;
        _caretOffset = clamped;

        if (resetVirtualColumn)
        {
            _virtualCaretColumn = GetCaretColumn ();
        }

        EnsureCaretVisible ();
        SetNeedsDraw ();

        if (changed)
        {
            CaretChanged?.Invoke (this, EventArgs.Empty);
        }
    }

    private void OnDocumentChanged (object? sender, DocumentChangeEventArgs e)
    {
        // AnchorMovementType.AfterInsertion semantics: an insert at the caret moves the caret past
        // the inserted text; an insert strictly after the caret leaves it alone; a removal that
        // straddles the caret snaps it to the removal start.
        if (_caretOffset >= e.Offset)
        {
            if (_caretOffset < e.Offset + e.RemovalLength)
            {
                _caretOffset = e.Offset;
            }
            else
            {
                _caretOffset = _caretOffset - e.RemovalLength + e.InsertionLength;
            }

            _virtualCaretColumn = GetCaretColumn ();
        }

        UpdateContentSize ();
        EnsureCaretVisible ();
        SetNeedsDraw ();
        DocumentChanged?.Invoke (this, e);
    }

    private void UpdateContentSize ()
    {
        if (_document == null)
        {
            return;
        }

        var maxWidth = _document.Lines.Select (line => line.Length).Prepend (0).Max ();

        // +1 column lets the caret sit just past the end-of-line.
        SetContentSize (new (maxWidth + 1, _document.LineCount));
    }

    private int GetCaretColumn ()
    {
        DocumentLine? line = _document?.GetLineByOffset (_caretOffset);

        return _caretOffset - (line?.Offset ?? 0);
    }

    private int GetCaretLineIndex ()
    {
        return _document?.GetLineByOffset (_caretOffset).LineNumber - 1 ?? 0;
    }

    /// <summary>
    ///     Moves the caret <paramref name="delta"/> lines, preserving the sticky virtual column when
    ///     traversing shorter lines (i.e. snap back to the original column on the next long-enough line).
    /// </summary>
    private void MoveCaretVertically (int delta)
    {
        int targetLine = Math.Clamp (GetCaretLineIndex () + delta, 0, _document!.LineCount - 1);
        DocumentLine line = _document!.GetLineByNumber (targetLine + 1);
        int targetCol = Math.Min (_virtualCaretColumn, line.Length);

        // Preserve the sticky column across vertical moves — SetCaretOffset would otherwise reset it.
        int sticky = _virtualCaretColumn;
        SetCaretOffset (line.Offset + targetCol, resetVirtualColumn: false);
        _virtualCaretColumn = sticky;
    }

    private void EnsureCaretVisible ()
    {
        Rectangle viewport = Viewport;

        if (viewport.Width == 0 || viewport.Height == 0)
        {
            return;
        }

        var caretLine = GetCaretLineIndex ();
        var caretCol = GetCaretColumn ();
        var newY = viewport.Y;
        var newX = viewport.X;

        if (caretLine < newY)
        {
            newY = caretLine;
        }
        else if (caretLine >= newY + viewport.Height)
        {
            newY = caretLine - viewport.Height + 1;
        }

        if (caretCol < newX)
        {
            newX = caretCol;
        }
        else if (caretCol >= newX + viewport.Width)
        {
            newX = caretCol - viewport.Width + 1;
        }

        if (newX != viewport.X || newY != viewport.Y)
        {
            Viewport = viewport with { X = newX, Y = newY };
        }
    }
}

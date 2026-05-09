using System.ComponentModel;
using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

/// <summary>
///     Single-document text editor View backed by <see cref="TextDocument" />. Renders multi-line
///     text from a rope-backed document, tracks a caret offset, dispatches keyboard input to
///     navigate / edit, and scrolls content when it exceeds the viewport. Pre-MVP — selection,
///     folding, syntax highlighting still pending per <c>specs/00-plan.md</c>.
/// </summary>
public partial class Editor : View
{
    private int _caretOffset;
    private TextDocument? _document;
    private bool _showLineNumbers;
    private ISyntaxHighlighter? _syntaxHighlighter;
    private string _syntaxLanguage = "csharp";
    private int _tabWidth = 4;

    /// <summary>
    ///     Sticky column for vertical caret moves. Tracks the column the user *intends* to be in,
    ///     even when the current line is shorter, so Up/Down across short lines snap back to the
    ///     original column on the first long line.
    /// </summary>
    private int _virtualCaretColumn;

    /// <summary>Initializes a new <see cref="Editor" /> with an empty <see cref="TextDocument" />.</summary>
    public Editor ()
    {
        CanFocus = true;
        CreateCommandsAndBindings ();
        Document = new ();
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
            UpdateLineNumberPadding ();
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

    /// <summary>
    ///     Gets or sets whether one-based line numbers are rendered in the editor's left padding.
    /// </summary>
    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            if (_showLineNumbers == value)
            {
                return;
            }

            _showLineNumbers = value;
            UpdateLineNumberPadding ();
            SetNeedsDraw ();
        }
    }

    /// <summary>Optional syntax highlighter used when drawing document text.</summary>
    ///     Optional syntax highlighter used when drawing document text.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Stopgap.</b> This property reuses Terminal.Gui's
    ///         <see cref="ISyntaxHighlighter" /> from <c>Terminal.Gui.Drawing.Markdown</c>, which is
    ///         shaped for Markdown rendering — not for an editor's per-line / per-visual-line
    ///         highlighting pipeline. It will be removed when <c>specs/00-plan.md</c> Phase 6 lifts
    ///         AvaloniaEdit's <c>Highlighting/</c> folder and the editor switches to a
    ///         <c>HighlightingColorizer : IVisualLineTransformer</c> running over the
    ///         <see cref="DocumentLine" /> → visual-line pipeline tracked by issue #28.
    ///     </para>
    ///     <para>
    ///         External code should not take a hard dependency on this contract.
    ///     </para>
    /// </remarks>
    [Obsolete (
        "Stopgap reusing Terminal.Gui's Markdown ISyntaxHighlighter; will be replaced by HighlightingColorizer when specs/00-plan.md Phase 6 lifts AvaloniaEdit's Highlighting/ folder. See issue #28 for the visual-line pipeline that replaces this. Tracked by issue #32.")]
    [EditorBrowsable (EditorBrowsableState.Never)]
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
    /// <remarks>
    ///     Obsolete for the same reason as <see cref="SyntaxHighlighter" /> — this is part of the
    ///     temporary Markdown-shaped surface that Phase 6 will replace. See issue #28 / #32.
    /// </remarks>
    [Obsolete (
        "Stopgap reusing Terminal.Gui's Markdown ISyntaxHighlighter; will be replaced by HighlightingColorizer when specs/00-plan.md Phase 6 lifts AvaloniaEdit's Highlighting/ folder. See issue #28 for the visual-line pipeline that replaces this. Tracked by issue #32.")]
    [EditorBrowsable (EditorBrowsableState.Never)]
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

    /// <summary>Visual tab-stop width in cells. Defaults to 4.</summary>
    public int TabWidth
    {
        get => _tabWidth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan (value, 1);

            if (_tabWidth == value)
            {
                return;
            }

            _tabWidth = value;
            _virtualCaretColumn = GetCaretColumn ();
            UpdateContentSize ();
            EnsureCaretVisible ();
            SetNeedsDraw ();
        }
    }

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

    /// <inheritdoc />
    protected override void Dispose (bool disposing)
    {
        if (disposing && _document is not null)
        {
            // Without this the document keeps the editor alive via the Changed subscription whenever
            // external code retains the TextDocument (test fixtures, future shared docs across panes,
            // etc.). The Document setter unsubscribes on swap; this covers View-teardown.
            _document.Changed -= OnDocumentChanged;
        }

        base.Dispose (disposing);
    }

    private void OnDocumentChanged (object? sender, DocumentChangeEventArgs e)
    {
        // Content-size has to refresh first so EnsureCaretVisible inside SetCaretOffset clamps the
        // viewport against the new line count.
        UpdateContentSize ();

        // Manual stand-in for TextAnchor.AfterInsertion until specs/00-plan.md §6 lands. The math:
        // an insert at-or-before the caret pushes it forward by InsertionLength; an insert strictly
        // after the caret leaves it alone; a removal that straddles the caret snaps it to the
        // removal start; a removal entirely before the caret slides it back by RemovalLength.
        if (_caretOffset >= e.Offset)
        {
            int target = _caretOffset < e.Offset + e.RemovalLength
                             ? e.Offset
                             : _caretOffset - e.RemovalLength + e.InsertionLength;

            // Route through SetCaretOffset so CaretChanged fires when the caret actually moves.
            // SetCaretOffset also handles EnsureCaretVisible + SetNeedsDraw.
            SetCaretOffset (target, true);

            return;
        }

        UpdateContentSize ();
        UpdateLineNumberPadding ();
        EnsureCaretVisible ();
        SetNeedsDraw ();
    }

    private void UpdateContentSize ()
    {
        if (_document == null)
        {
            return;
        }

        var maxWidth = _document.Lines.Select (line => GetVisualColumnFromLogicalColumn (line, line.Length)).Prepend (0).Max ();

        // +1 column lets the caret sit just past the end-of-line.
        SetContentSize (new (maxWidth + 1, _document.LineCount));
    }

    private void UpdateLineNumberPadding ()
    {
        Thickness thickness = Padding.Thickness;
        int left = _showLineNumbers && _document is not null ? GetLineNumberPaddingWidth () : 0;

        if (thickness.Left == left)
        {
            return;
        }

        Padding.Thickness = new (left, thickness.Top, thickness.Right, thickness.Bottom);
    }

    private int GetLineNumberPaddingWidth ()
    {
        int lineCount = Math.Max (1, _document?.LineCount ?? 1);

        return lineCount.ToString ().Length + 1;
    }

    private int GetCaretColumn ()
    {
        DocumentLine? line = _document?.GetLineByOffset (_caretOffset);

        if (line is null)
        {
            return 0;
        }

        int logicalColumn = _caretOffset - line.Offset;

        return GetVisualColumnFromLogicalColumn (line, logicalColumn);
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
        int targetCol = GetLogicalColumnFromVisualColumn (line, _virtualCaretColumn);

        // resetVirtualColumn: false keeps the sticky column intact across vertical moves.
        SetCaretOffset (line.Offset + targetCol, resetVirtualColumn: false);
    }

    private int GetVisualColumnFromLogicalColumn (DocumentLine line, int logicalColumn)
    {
        string text = _document!.GetText (line);
        int clampedLogical = Math.Clamp (logicalColumn, 0, text.Length);
        int visualColumn = 0;

        for (int i = 0; i < clampedLogical; i++)
        {
            visualColumn += GetVisualWidthForCharacter (text[i], visualColumn, TabWidth);
        }

        return visualColumn;
    }

    private int GetLogicalColumnFromVisualColumn (DocumentLine line, int visualColumn)
    {
        string text = _document!.GetText (line);
        int clampedVisual = Math.Max (0, visualColumn);
        int currentVisual = 0;

        for (int logical = 0; logical < text.Length; logical++)
        {
            int width = GetVisualWidthForCharacter (text[logical], currentVisual, TabWidth);
            int nextVisual = currentVisual + width;

            if (nextVisual >= clampedVisual)
            {
                if (text[logical] == '\t' && clampedVisual > currentVisual)
                {
                    // Clicking or moving inside the visual span produced by '\t' snaps the caret
                    // after the tab character because there is no representable position "inside"
                    // a single tab code point.
                    return logical + 1;
                }

                return clampedVisual >= nextVisual ? logical + 1 : logical;
            }

            currentVisual = nextVisual;
        }

        return text.Length;
    }

    private static int GetVisualWidthForCharacter (char c, int visualColumn, int tabWidth)
    {
        if (c != '\t')
        {
            return 1;
        }

        int remainder = visualColumn % tabWidth;

        return remainder == 0 ? tabWidth : tabWidth - remainder;
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

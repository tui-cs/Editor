using System.ComponentModel;
using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views.Rendering;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Views;

/// <summary>
///     Single-document text editor View backed by <see cref="TextDocument" />. Renders multi-line
///     text from a rope-backed document, tracks a caret offset, dispatches keyboard input to
///     navigate / edit, and scrolls content when it exceeds the viewport. Pre-MVP — selection,
///     folding, syntax highlighting still pending per <c>specs/00-plan.md</c>.
/// </summary>
public partial class Editor : View
{
    // Two narrow caches keyed by DocumentLine.LineNumber. Kept separate so the caret path and
    // the draw path don't thrash each other's entries (different attribute sets per call site).
    //
    //   _defaultVisualLineCache: built with Attribute.Default, no selection / no segments. Hit by
    //     caret math, mouse hit-testing, indentation, UpdateContentSize's all-lines walk, Gutter.
    //
    //   _drawVisualLineCache: built with the editor's role attributes for the draw path, but only
    //     when no syntax segments / no selection / no transformers are in play — i.e. plain-text
    //     scrolling without a highlighter. Idea from PR #54: a consumer that doesn't enable
    //     syntax highlighting otherwise rebuilds every visible line on every frame.
    //
    // Both invalidate together: ranged in OnDocumentChanged (from the first affected line down)
    // and wholesale on Document swap / IndentationSize / ShowTabs / (draw cache only) attribute
    // role mismatch.
    private readonly Dictionary<int, CellVisualLine> _defaultVisualLineCache = [];
    private readonly Dictionary<int, DrawCacheEntry> _drawVisualLineCache = [];
    private readonly VisualLineBuilder _visualLineBuilder = new ();

    private int _caretOffset;
    private TextDocument? _document;
    private Gutter? _gutter;
    private bool _showLineNumbers;
    private ISyntaxHighlighter? _syntaxHighlighter;

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
        Document = new TextDocument ();
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
            ClearVisualLineCaches ();

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

    /// <summary>
    ///     Optional syntax highlighter used when drawing document text.
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
        get;
        set
        {
            ArgumentNullException.ThrowIfNull (value);

            if (field == value)
            {
                return;
            }

            field = value;
            _syntaxHighlighter?.ResetState ();
            SetNeedsDraw ();
        }
    } = "csharp";

    /// <summary>Width of one indentation unit, in terminal cells. Defaults to 4.</summary>
    public int IndentationSize
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan (value, 1);

            if (field == value)
            {
                return;
            }

            field = value;
            ClearVisualLineCaches ();
            _virtualCaretColumn = GetCaretColumn ();
            UpdateContentSize ();
            EnsureCaretVisible ();
            SetNeedsDraw ();
        }
    } = 4;

    /// <summary>Whether pressing Tab inserts spaces instead of a tab character.</summary>
    public bool ConvertTabsToSpaces { get; set; }

    /// <summary>Whether tab characters render with a visible glyph in their first cell.</summary>
    public bool ShowTabs
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            ClearVisualLineCaches ();
            SetNeedsDraw ();
        }
    }

    /// <summary>Transformers applied to visual lines after they are built.</summary>
    public IList<IVisualLineTransformer> LineTransformers { get; } = [];

    /// <summary>Background renderers drawn before visual-line elements.</summary>
    public IList<IBackgroundRenderer> BackgroundRenderers { get; } = [];

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
        // Drop cached visual lines whose content the change could have touched. The change
        // affects the line containing e.Offset; if the insertion/removal includes newlines,
        // line numbers downstream may also have shifted, so clear everything from that line on.
        // Cheap: usually one or a handful of entries; correctness > saving a few cache hits.
        InvalidateVisualLineCaches (e);

        // Content-size has to refresh first so EnsureCaretVisible inside SetCaretOffset clamps the
        // viewport against the new line count.
        UpdateContentSize ();

        // Manual stand-in for TextAnchor.AfterInsertion until specs/00-plan.md §6 lands. The math:
        // an insert at-or-before the caret pushes it forward by InsertionLength; an insert strictly
        // after the caret leaves it alone; a removal that straddles the caret snaps it to the
        // removal start; a removal entirely before the caret slides it back by RemovalLength.
        if (_caretOffset >= e.Offset)
        {
            var target = _caretOffset < e.Offset + e.RemovalLength
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

        var maxWidth = 0;

        foreach (DocumentLine line in _document.Lines)
        {
            var width = GetOrBuildDefaultVisualLine (line).VisualLength;

            if (width > maxWidth)
            {
                maxWidth = width;
            }
        }

        // +1 column lets the caret sit just past the end-of-line.
        SetContentSize (new Size (maxWidth + 1, _document.LineCount));
    }

    private void ClearVisualLineCaches ()
    {
        _defaultVisualLineCache.Clear ();
        _drawVisualLineCache.Clear ();
    }

    private void InvalidateVisualLineCaches (DocumentChangeEventArgs e)
    {
        if (_document is null || (_defaultVisualLineCache.Count == 0 && _drawVisualLineCache.Count == 0))
        {
            return;
        }

        // The change starts at e.Offset; anything before that line is unaffected. The cheapest
        // sound invalidation is "drop entries for line numbers ≥ the changed line's number" —
        // newline insert/delete renumbers everything downstream, so per-line keys past the edit
        // are stale even if their content is untouched.
        DocumentLine firstAffected = _document.GetLineByOffset (Math.Min (e.Offset, _document.TextLength));
        var threshold = firstAffected.LineNumber;

        RemoveFromCache (_defaultVisualLineCache, threshold);
        RemoveFromCache (_drawVisualLineCache, threshold);

        static void RemoveFromCache<TValue> (Dictionary<int, TValue> cache, int threshold)
        {
            if (cache.Count == 0)
            {
                return;
            }

            List<int>? toRemove = null;

            foreach (var lineNumber in cache.Keys)
            {
                if (lineNumber >= threshold)
                {
                    (toRemove ??= []).Add (lineNumber);
                }
            }

            if (toRemove is null)
            {
                return;
            }

            foreach (var lineNumber in toRemove)
            {
                cache.Remove (lineNumber);
            }
        }
    }

    private void UpdateLineNumberPadding ()
    {
        Thickness thickness = Padding.Thickness;
        var left = _showLineNumbers && _document is not null ? GetLineNumberPaddingWidth () : 0;

        if (thickness.Left != left)
        {
            Padding.Thickness = new Thickness (left, thickness.Top, thickness.Right, thickness.Bottom);
        }

        SyncGutter (left);
    }

    private void SyncGutter (int left)
    {
        if (left > 0)
        {
            if (_gutter is null)
            {
                // Hosting Gutter as a SubView of Padding (instead of painting in
                // OnDrawComplete via the driver) keeps it inside the View hierarchy, so popovers
                // and menus correctly clip it instead of being drawn over.
                _gutter = new Gutter (this)
                {
                    X = 0,
                    Y = 0,
                    Width = left,
                    Height = Dim.Fill ()
                };
                Padding.GetOrCreateView ().Add (_gutter);
            }
            else
            {
                _gutter.Width = left;
                _gutter.SetNeedsDraw ();
            }
        }
        else if (_gutter is not null)
        {
            Padding.GetOrCreateView ().Remove (_gutter);
            _gutter.Dispose ();
            _gutter = null;
        }
    }

    private int GetLineNumberPaddingWidth ()
    {
        var lineCount = Math.Max (1, _document?.LineCount ?? 1);

        return lineCount.ToString ().Length + 1;
    }

    private int GetCaretColumn ()
    {
        DocumentLine? line = _document?.GetLineByOffset (_caretOffset);

        return line is null ? 0 : GetOrBuildDefaultVisualLine (line).GetVisualColumn (_caretOffset - line.Offset);
    }

    private int GetCaretLineIndex ()
    {
        return _document?.GetLineByOffset (_caretOffset).LineNumber - 1 ?? 0;
    }

    /// <summary>
    ///     Moves the caret <paramref name="delta" /> lines, preserving the sticky virtual column when
    ///     traversing shorter lines (i.e. snap back to the original column on the next long-enough line).
    /// </summary>
    private void MoveCaretVertically (int delta)
    {
        var targetLine = Math.Clamp (GetCaretLineIndex () + delta, 0, _document!.LineCount - 1);
        DocumentLine line = _document!.GetLineByNumber (targetLine + 1);
        var targetCol = GetOrBuildDefaultVisualLine (line).GetRelativeOffset (_virtualCaretColumn);

        // resetVirtualColumn: false keeps the sticky column intact across vertical moves.
        SetCaretOffset (line.Offset + targetCol, false);
    }

    /// <summary>
    ///     Builds (or returns the cached) <see cref="CellVisualLine" /> with default attributes / no
    ///     selection / no syntax segments. Used by every caller that needs visual-column geometry
    ///     but not styled cell content: caret math, mouse hit-testing, indentation, the all-lines
    ///     walk in <see cref="UpdateContentSize" />, and <see cref="Gutter" />.
    /// </summary>
    private CellVisualLine GetOrBuildDefaultVisualLine (DocumentLine line)
    {
        if (_defaultVisualLineCache.TryGetValue (line.LineNumber, out CellVisualLine? cached))
        {
            return cached;
        }

        CellVisualLine built = BuildVisualLine (line);
        _defaultVisualLineCache[line.LineNumber] = built;

        return built;
    }

    /// <summary>
    ///     Returns a cached draw-path <see cref="CellVisualLine" /> when the call is eligible
    ///     (no segments, no selection on this line, no transformers, attributes match the cached
    ///     entry). Otherwise builds fresh and — if eligible — stores it. The draw cache only fires
    ///     for consumers that don't enable syntax highlighting; once segments are present every
    ///     call falls through to a fresh build.
    /// </summary>
    private CellVisualLine GetOrBuildDrawVisualLine (
        DocumentLine line,
        IReadOnlyList<StyledSegment>? segments,
        Attribute normal,
        Attribute selected,
        int selStart,
        int selEnd)
    {
        if (!IsDrawCacheEligible (segments, selStart, selEnd))
        {
            return BuildVisualLine (line, segments, normal, selected, selStart, selEnd);
        }

        if (_drawVisualLineCache.TryGetValue (line.LineNumber, out DrawCacheEntry cached)
            && cached.Normal == normal
            && cached.Selected == selected)
        {
            return cached.Line;
        }

        CellVisualLine built = BuildVisualLine (line, segments, normal, selected, selStart, selEnd);
        _drawVisualLineCache[line.LineNumber] = new DrawCacheEntry (built, normal, selected);

        return built;
    }

    private bool IsDrawCacheEligible (IReadOnlyList<StyledSegment>? segments, int selStart, int selEnd)
    {
        return segments is null && selStart >= selEnd && LineTransformers.Count == 0;
    }

    private readonly record struct DrawCacheEntry (CellVisualLine Line, Attribute Normal, Attribute Selected);

    private CellVisualLine BuildVisualLine (
        DocumentLine line,
        IReadOnlyList<StyledSegment>? styledSegments = null,
        Attribute? normalAttribute = null,
        Attribute? selectedAttribute = null,
        int selectionStart = 0,
        int selectionEnd = 0)
    {
        VisualLineBuildContext context = new (
            _document!,
            IndentationSize,
            ShowTabs,
            normalAttribute ?? Attribute.Default,
            selectedAttribute ?? Attribute.Default,
            styledSegments,
            selectionStart,
            selectionEnd,
            LineTransformers);

        return _visualLineBuilder.Build (line, context);
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

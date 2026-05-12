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
///     navigate / edit, and scrolls content when it exceeds the viewport. Pre-MVP — folding,
///     syntax highlighting, and multi-caret still pending per <c>specs/plan.md</c>.
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

    // Incremental max-width tracking: avoids the O(N) all-lines walk that UpdateContentSize
    // used to do on every edit. _maxVisualWidth is the widest visual line seen; _maxWidthLineNumber
    // tracks which line holds it so we can detect when that line is edited. _maxWidthDirty forces
    // a full recompute (e.g. on Document swap or IndentationSize change).
    private int _maxVisualWidth;
    private int _maxWidthLineNumber;
    private bool _maxWidthDirty = true;

    private TextAnchor? _caretAnchor;
    private TextDocument? _document;
    private Gutter? _gutter;
    private int _lastKnownCaretOffset;
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

            var caretOffset = Math.Clamp (CaretOffset, 0, value.TextLength);
            var hadSelection = HasSelection;
            (int start, int end) beforeSelection = SelectionTuple ();

            _document = value;
            _document.Changed += OnDocumentChanged;
            _caretAnchor = CreateCaretAnchor (caretOffset);
            _lastKnownCaretOffset = caretOffset;
            _selectionAnchor = null;
            ClearVisualLineCaches ();
            _maxWidthDirty = true;

            _virtualCaretColumn = GetCaretColumn ();
            UpdateContentSize ();
            UpdateLineNumberPadding ();

            if (hadSelection)
            {
                RaiseSelectionChangedIfMoved (beforeSelection);
            }

            SetNeedsDraw ();
        }
    }

    /// <summary>
    ///     Current caret offset. Clamped to <c>[0, Document.TextLength]</c>. Setting this scrolls the
    ///     viewport to keep the caret visible and raises <see cref="CaretChanged" />.
    /// </summary>
    public int CaretOffset
    {
        get => GetCaretOffset ();
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
    ///     Gets or sets whether editor commands are allowed to modify the document.
    ///     Navigation and selection commands continue to work while read-only.
    /// </summary>
    public bool ReadOnly { get; set; }

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
            _maxWidthDirty = true;
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
        var current = CaretOffset;

        if (clamped == current && !resetVirtualColumn)
        {
            return;
        }

        var changed = clamped != current;
        _caretAnchor = _document is null ? null : CreateCaretAnchor (clamped);
        _lastKnownCaretOffset = clamped;

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
            _lastKnownCaretOffset = CaretOffset;
            _caretAnchor = null;
            _selectionAnchor = null;
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
        InvalidateHighlighterState (e);
        UpdateMaxWidthIncremental (e);
        UpdateContentSize ();
        UpdateLineNumberPadding ();

        var current = CaretOffset;

        if (current != _lastKnownCaretOffset)
        {
            _lastKnownCaretOffset = current;
            _virtualCaretColumn = GetCaretColumn ();
            CaretChanged?.Invoke (this, EventArgs.Empty);
        }

        RefreshSelectionAnchorMovement ();
        EnsureCaretVisible ();
        SetNeedsDraw ();
    }

    private void UpdateContentSize ()
    {
        if (_document == null)
        {
            return;
        }

        if (_maxWidthDirty)
        {
            RecomputeMaxWidth ();
        }

        // +1 column lets the caret sit just past the end-of-line.
        SetContentSize (new Size (_maxVisualWidth + 1, _document.LineCount));
    }

    /// <summary>Full O(N) recompute — only called on Document swap, IndentationSize change, etc.</summary>
    private void RecomputeMaxWidth ()
    {
        _maxVisualWidth = 0;
        _maxWidthLineNumber = 0;

        if (_document is null)
        {
            _maxWidthDirty = false;

            return;
        }

        foreach (DocumentLine line in _document.Lines)
        {
            var width = GetOrBuildDefaultVisualLine (line).VisualLength;

            if (width > _maxVisualWidth)
            {
                _maxVisualWidth = width;
                _maxWidthLineNumber = line.LineNumber;
            }
        }

        _maxWidthDirty = false;
    }

    /// <summary>
    ///     Incrementally updates max width after a document change. Only recomputes the affected
    ///     lines. If the edited line was the widest, does a full recompute since the max may have shrunk.
    /// </summary>
    private void UpdateMaxWidthIncremental (DocumentChangeEventArgs e)
    {
        if (_document is null)
        {
            return;
        }

        // Find which lines are affected by the change.
        DocumentLine firstAffected = _document.GetLineByOffset (Math.Min (e.Offset, _document.TextLength));
        var insertedText = e.InsertedText?.Text ?? "";
        var newlineCount = insertedText.Count (c => c == '\n');
        var removedText = e.RemovedText?.Text ?? "";
        var removedNewlines = removedText.Count (c => c == '\n');

        // If the widest line was deleted or its content changed, we must recompute.
        if (_maxWidthLineNumber >= firstAffected.LineNumber
            && (_maxWidthLineNumber <= firstAffected.LineNumber + Math.Max (removedNewlines, 0)
                || removedNewlines > 0))
        {
            // The max-holder was touched or lines were removed — check affected lines first,
            // and only fall back to full recompute if the old max shrank.
            var newMax = 0;
            var newMaxLine = _maxWidthLineNumber;

            // Scan from firstAffected through the new lines that were inserted.
            var scanEnd = Math.Min (firstAffected.LineNumber + newlineCount, _document.LineCount);

            for (var lineNum = firstAffected.LineNumber; lineNum <= scanEnd; lineNum++)
            {
                DocumentLine line = _document.GetLineByNumber (lineNum);
                var width = GetOrBuildDefaultVisualLine (line).VisualLength;

                if (width >= newMax)
                {
                    newMax = width;
                    newMaxLine = lineNum;
                }
            }

            if (newMax >= _maxVisualWidth)
            {
                // The affected region has a line at least as wide — it's the new max.
                _maxVisualWidth = newMax;
                _maxWidthLineNumber = newMaxLine;
            }
            else
            {
                // The old widest line shrank and no scanned line is as wide — some unscanned
                // line may be the new widest. Fall back to full recompute.
                _maxWidthDirty = true;
            }

            return;
        }

        // The change didn't touch the widest line. Just check affected lines for a new max.
        var endLine = Math.Min (firstAffected.LineNumber + newlineCount, _document.LineCount);

        for (var lineNum = firstAffected.LineNumber; lineNum <= endLine; lineNum++)
        {
            DocumentLine line = _document.GetLineByNumber (lineNum);
            var width = GetOrBuildDefaultVisualLine (line).VisualLength;

            if (width > _maxVisualWidth)
            {
                _maxVisualWidth = width;
                _maxWidthLineNumber = lineNum;
            }
        }
    }

    private void ClearVisualLineCaches ()
    {
        _defaultVisualLineCache.Clear ();
        _drawVisualLineCache.Clear ();
    }

    /// <summary>
    ///     Resets the incremental highlighter state when a document change occurs at or before
    ///     the prepared-up-to line. Edits after the prepared region don't affect tokenizer state
    ///     for lines that have already been processed.
    /// </summary>
    private void InvalidateHighlighterState (DocumentChangeEventArgs e)
    {
        if (_highlighterPreparedUpToLine < 0 || _document is null)
        {
            return;
        }

        DocumentLine affectedLine = _document.GetLineByOffset (Math.Min (e.Offset, _document.TextLength));
        var affectedLineIndex = affectedLine.LineNumber - 1;

        if (affectedLineIndex < _highlighterPreparedUpToLine)
        {
            // Edit was before/within the prepared region — must re-prepare from line 0.
            // Setting to 0 (not -1) so PrepareSyntaxHighlighter's comparison triggers a
            // ResetState() call on the next draw frame.
            _highlighterPreparedUpToLine = 0;
            _highlighterPreparedInstance = null;
        }
    }

    private void InvalidateVisualLineCaches (DocumentChangeEventArgs e)
    {
        if (_document is null || (_defaultVisualLineCache.Count == 0 && _drawVisualLineCache.Count == 0))
        {
            return;
        }

        DocumentLine firstAffected = _document.GetLineByOffset (Math.Min (e.Offset, _document.TextLength));
        var threshold = firstAffected.LineNumber;

        // Count net newline delta: downstream line numbers shift by this amount.
        var insertedText = e.InsertedText?.Text ?? "";
        var insertedNewlines = insertedText.Count (c => c == '\n');
        var removedText = e.RemovedText?.Text ?? "";
        var removedNewlines = removedText.Count (c => c == '\n');
        var lineDelta = insertedNewlines - removedNewlines;

        RekeyCache (_defaultVisualLineCache, threshold, lineDelta, removedNewlines);
        RekeyCache (_drawVisualLineCache, threshold, lineDelta, removedNewlines);

        static void RekeyCache<TValue> (Dictionary<int, TValue> cache, int threshold, int lineDelta, int removedNewlines)
        {
            if (cache.Count == 0)
            {
                return;
            }

            // On newline removal, lines in [threshold, threshold + removedNewlines] were merged
            // and their cached content is stale — invalidate, don't rekey.
            var invalidateEnd = threshold + removedNewlines;

            // Collect entries: invalidate the edited line(s), rekey downstream.
            List<KeyValuePair<int, TValue>>? toRekey = null;
            List<int>? toRemove = null;

            foreach (KeyValuePair<int, TValue> kvp in cache)
            {
                if (kvp.Key >= threshold && kvp.Key <= invalidateEnd)
                {
                    // The edited/merged line(s) — content changed, must invalidate.
                    (toRemove ??= []).Add (kvp.Key);
                }
                else if (kvp.Key > invalidateEnd)
                {
                    if (lineDelta == 0)
                    {
                        // No newline change — downstream entries are still valid as-is.
                    }
                    else
                    {
                        // Line numbers shifted — remove old key, re-add with shifted key.
                        (toRemove ??= []).Add (kvp.Key);
                        (toRekey ??= []).Add (kvp);
                    }
                }
            }

            if (toRemove is not null)
            {
                foreach (var key in toRemove)
                {
                    cache.Remove (key);
                }
            }

            if (toRekey is not null)
            {
                foreach (KeyValuePair<int, TValue> kvp in toRekey)
                {
                    var newKey = kvp.Key + lineDelta;

                    if (newKey > 0)
                    {
                        cache[newKey] = kvp.Value;
                    }
                }
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
        var caretOffset = CaretOffset;
        DocumentLine? line = _document?.GetLineByOffset (caretOffset);

        return line is null ? 0 : GetOrBuildDefaultVisualLine (line).GetVisualColumn (caretOffset - line.Offset);
    }

    private int GetCaretLineIndex ()
    {
        return _document?.GetLineByOffset (CaretOffset).LineNumber - 1 ?? 0;
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

    private int GetCaretOffset ()
    {
        if (_caretAnchor is not { IsDeleted: false } anchor)
        {
            return _lastKnownCaretOffset;
        }

        return anchor.Offset;
    }

    private TextAnchor CreateCaretAnchor (int offset)
    {
        TextAnchor anchor = _document!.CreateAnchor (offset);
        anchor.MovementType = AnchorMovementType.AfterInsertion;
        anchor.SurviveDeletion = true;

        return anchor;
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

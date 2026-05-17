using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor.Rendering;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Text;
using Terminal.Gui.Text.Indentation;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor;

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

    private TextAnchor? _caretAnchor;
    private TextDocument? _document;
    private Gutter? _gutter;
    private GutterOptions _gutterOptions;
    private DocumentHighlighter? _highlighter;
    private HighlightingColorizer? _highlightingColorizer;
    private int _lastKnownCaretOffset;

    // Kill-ring: consecutive CutToEndOfLine / CutToStartOfLine appends to the clipboard instead
    // of replacing. Any non-kill command (including plain character insertion) breaks the run.
    //
    // _lastCommandWasKill is set to true by kill commands after executing.
    // _previousCommandWasKill is set by OnKeyDown (keyboard path) — it snapshots _lastCommandWasKill
    // before clearing it, so the dispatched kill command can read whether the preceding command was
    // a kill for append/prepend decisions.
    //
    // Keyboard path: OnKeyDown snapshots _lastCommandWasKill → _previousCommandWasKill, clears
    //   _lastCommandWasKill, then dispatches.  Kill commands read _previousCommandWasKill.
    // InvokeCommand path (programmatic): OnKeyDown is bypassed.  Kill commands fall back to
    //   _lastCommandWasKill directly.  Note: non-kill commands invoked via InvokeCommand do NOT
    //   clear _lastCommandWasKill, so a sequence like InvokeCommand(Kill) → InvokeCommand(Right) →
    //   InvokeCommand(Kill) will incorrectly append.  This is a known limitation of the
    //   programmatic path; keyboard dispatch (the primary use case) is unaffected.
    private bool _lastCommandWasKill;
    private bool _previousCommandWasKill;

    // Incremental max-width tracking: avoids the O(N) all-lines walk that UpdateContentSize
    // used to do on every edit. _maxVisualWidth is the widest visual line seen; _maxWidthLineNumber
    // tracks which line holds it so we can detect when that line is edited. _maxWidthDirty forces
    // a full recompute (e.g. on Document swap or IndentationSize change).
    private int _maxVisualWidth;
    private bool _maxWidthDirty = true;
    private int _maxWidthLineNumber;

    /// <summary>
    ///     Sticky column for vertical caret moves. Tracks the column the user *intends* to be in,
    ///     even when the current line is shorter, so Up/Down across short lines snap back to the
    ///     original column on the first long line.
    /// </summary>
    private int _virtualCaretColumn;

    // Word-wrap map: when WordWrap == true, maps visual row indices to (lineNumber, segmentIndex)
    // pairs. Lazily built and cached. Cleared on any document change or property change that
    // affects wrapping. _wrapMapColumn tracks the wrap column used to build the map so we
    // can detect viewport-width changes and rebuild.
    private List<WrapMapEntry>? _wrapMap;
    private int _wrapMapColumn;

    /// <summary>Initializes a new <see cref="Editor" /> with an empty <see cref="TextDocument" />.</summary>
    public Editor ()
    {
        CanFocus = true;
        CreateCommandsAndBindings ();
        OverlayRenderers.Add (new MultiCaretRenderer (this));
        Document = new TextDocument ();
        ThemeManager.ThemeChanged += OnThemeChanged;
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
            _additionalCarets.Clear ();
            ClearVisualLineCaches ();
            _cachedVisibleLineNumbers = null;
            _maxWidthDirty = true;

            InstallHighlighter ();

            _virtualCaretColumn = GetCaretColumn ();
            UpdateContentSize ();
            UpdateGutterWidth ();

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
    ///     Gets or sets which elements the gutter displays. Combine flags to show multiple elements:
    ///     <c>GutterOptions.LineNumbers | GutterOptions.Folding</c>.
    /// </summary>
    public GutterOptions GutterOptions
    {
        get => _gutterOptions;
        set
        {
            if (_gutterOptions == value)
            {
                return;
            }

            _gutterOptions = value;
            UpdateGutterWidth ();
            SetNeedsDraw ();
        }
    }

    /// <summary>
    ///     Gets or sets whether editor commands are allowed to modify the document.
    ///     Navigation and selection commands continue to work while read-only.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    ///     Gets or sets the highlighting definition used for syntax coloring. When set, a
    ///     <see cref="HighlightingColorizer" /> is automatically added to
    ///     <see cref="LineTransformers" />. Set to <see langword="null" /> to disable
    ///     syntax highlighting.
    /// </summary>
    /// <remarks>
    ///     Use <see cref="HighlightingManager.Instance" /> to look up definitions by name
    ///     or file extension:
    ///     <code>editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinitionByExtension (".cs");</code>
    /// </remarks>
    public IHighlightingDefinition? HighlightingDefinition
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            InstallHighlighter ();
            SetNeedsDraw ();
        }
    }

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

    /// <summary>
    ///     Gets or sets the indentation strategy applied when Enter is pressed.
    ///     When non-null, the strategy's <see cref="IIndentationStrategy.IndentLine" /> is called
    ///     on the newly created line to copy (or compute) indentation from the previous line.
    ///     Defaults to <see cref="DefaultIndentationStrategy" />.
    ///     Set to <see langword="null" /> to disable auto-indent on Enter.
    /// </summary>
    public IIndentationStrategy? IndentationStrategy { get; set; } = new DefaultIndentationStrategy ();

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

    /// <summary>
    ///     When <see langword="true" />, lines wider than the viewport soft-wrap at whitespace
    ///     boundaries (or hard-break when no whitespace exists). Continuation lines render flush
    ///     at column 0. Defaults to <see langword="false" />.
    /// </summary>
    public bool WordWrap
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
            _cachedVisibleLineNumbers = null;
            _wrapMap = null;
            _maxWidthDirty = true;
            _virtualCaretColumn = GetCaretColumn ();
            UpdateContentSize ();
            EnsureCaretVisible ();
            SetNeedsDraw ();
        }
    }

    /// <summary>Transformers applied to visual lines after they are built.</summary>
    public IList<IVisualLineTransformer> LineTransformers { get; } = [];

    /// <summary>Background renderers drawn before visual-line elements.</summary>
    public IList<IBackgroundRenderer> BackgroundRenderers { get; } = [];

    /// <summary>Overlay renderers drawn after visual-line elements (on top of text).</summary>
    public IList<IOverlayRenderer> OverlayRenderers { get; } = [];

    /// <summary>
    ///     Gets or sets the <see cref="Document.Folding.FoldingManager" /> that tracks collapsible regions.
    ///     Setting this installs a <see cref="FoldingTransformer" /> and subscribes to fold change events.
    /// </summary>
    public FoldingManager? FoldingManager
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            if (field is not null)
            {
                field.FoldingChanged -= OnFoldingChanged;

                // Remove the folding transformer installed by the previous manager.
                for (var i = LineTransformers.Count - 1; i >= 0; i--)
                {
                    if (LineTransformers[i] is FoldingTransformer)
                    {
                        LineTransformers.RemoveAt (i);
                    }
                }
            }

            field = value;

            if (field is not null)
            {
                field.FoldingChanged += OnFoldingChanged;
                LineTransformers.Insert (0, new FoldingTransformer (field));
            }

            ClearVisualLineCaches ();
            UpdateContentSize ();
            UpdateGutterWidth ();
            SetNeedsDraw ();
        }
    }

    /// <summary>
    ///     Re-renders with the new TG theme's attributes when <see cref="ThemeManager.Theme" />
    ///     changes. Visual-line caches bake in resolved attributes, so they are dropped.
    /// </summary>
    private void OnThemeChanged (object? sender, EventArgs<string> e)
    {
        ClearVisualLineCaches ();
        SetNeedsDraw ();
    }

    private void OnFoldingChanged (object? sender, EventArgs e)
    {
        ClearVisualLineCaches ();
        _cachedVisibleLineNumbers = null;
        _wrapMap = null;
        _maxWidthDirty = true;
        UpdateContentSize ();
        SetNeedsDraw ();
        _gutter?.SetNeedsDraw ();
    }

    /// <summary>
    ///     If the caret is inside a collapsed fold, expand it so the caret stays visible.
    /// </summary>
    private void EnsureCaretNotInFold ()
    {
        if (FoldingManager is not { } fm)
        {
            return;
        }

        var caretOffset = CaretOffset;

        foreach (FoldingSection fs in fm.GetFoldingsContaining (caretOffset))
        {
            if (fs.IsFolded && fs.StartOffset < caretOffset && caretOffset < fs.EndOffset)
            {
                fs.IsFolded = false;
            }
        }
    }

    /// <summary>
    ///     Gets or sets whether the editor is in overwrite mode. When <see langword="true" />, typed
    ///     characters replace the grapheme under the caret instead of inserting before it. At line-end
    ///     or when a selection is active, the insertion still inserts. Defaults to <see langword="false" />.
    /// </summary>
    public bool OverwriteMode
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            SetNeedsDraw ();
            OverwriteModeChanged?.Invoke (this, EventArgs.Empty);
        }
    }

    /// <summary>Raised whenever <see cref="OverwriteMode" /> changes.</summary>
    public event EventHandler? OverwriteModeChanged;

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

        // The primary just moved. Re-establish the multi-caret invariant *before* the next edit
        // applies: drop any additional caret that now coincides with the primary so a primary
        // landing on an additional caret doesn't produce a duplicate insert.
        NormalizeAdditionalCarets ();

        if (resetVirtualColumn)
        {
            _virtualCaretColumn = GetCaretColumn ();
        }

        EnsureCaretVisible ();
        EnsureCaretNotInFold ();
        SetNeedsDraw ();

        if (changed)
        {
            CaretChanged?.Invoke (this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    protected override void Dispose (bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }

        if (disposing && _document is not null)
        {
            // Without this the document keeps the editor alive via the Changed subscription whenever
            // external code retains the TextDocument (test fixtures, future shared docs across panes,
            // etc.). The Document setter unsubscribes on swap; this covers View-teardown.
            _document.Changed -= OnDocumentChanged;
            _lastKnownCaretOffset = CaretOffset;
            _caretAnchor = null;
            _selectionAnchor = null;
            _additionalCarets.Clear ();
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
        _cachedVisibleLineNumbers = null;
        _searchHitRenderer?.Invalidate ();
        _wrapMap = null;
        UpdateMaxWidthIncremental (e);
        UpdateContentSize ();
        UpdateGutterWidth ();

        var current = CaretOffset;

        if (current != _lastKnownCaretOffset)
        {
            _lastKnownCaretOffset = current;
            _virtualCaretColumn = GetCaretColumn ();
            CaretChanged?.Invoke (this, EventArgs.Empty);
        }

        RefreshSelectionAnchorMovement ();

        // Document structure changed — re-establish the multi-caret invariant before the next
        // edit (drop deleted / duplicate / primary-coinciding additional carets).
        NormalizeAdditionalCarets ();

        EnsureCaretVisible ();
        SetNeedsDraw ();
    }

    private void UpdateContentSize ()
    {
        if (_document == null)
        {
            return;
        }

        if (WordWrap)
        {
            // In word-wrap mode, the content height is the total number of visual rows
            // (each wrapped segment counts as one row). No horizontal scrolling needed.
            List<WrapMapEntry> map = GetWrapMap ();
            SetContentSize (new Size (Viewport.Width, Math.Max (1, map.Count)));

            return;
        }

        if (_maxWidthDirty)
        {
            RecomputeMaxWidth ();
        }

        // +1 column lets the caret sit just past the end-of-line.
        var visibleLines = _document.LineCount - (FoldingManager?.GetHiddenLineCount () ?? 0);
        SetContentSize (new Size (_maxVisualWidth + 1, Math.Max (1, visibleLines)));
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
        var insertedText = e.InsertedText.Text;
        var newlineCount = insertedText.Count (c => c == '\n');
        var removedText = e.RemovedText.Text;
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
    ///     Invalidates the <see cref="DocumentHighlighter" /> state when a document change
    ///     occurs. The <see cref="DocumentHighlighter" /> implements <see cref="ILineTracker" />
    ///     and handles incremental invalidation internally, so no per-edit work is needed here
    ///     beyond clearing the visual-line caches (handled separately).
    /// </summary>
    private void InvalidateHighlighterState (DocumentChangeEventArgs _)
    {
        // DocumentHighlighter (an ILineTracker) is notified of edits automatically
        // by the TextDocument. No explicit invalidation needed.
    }

    /// <summary>
    ///     Creates or tears down the <see cref="DocumentHighlighter" /> and
    ///     <see cref="HighlightingColorizer" /> when the highlighting definition or document
    ///     changes. Keeps <see cref="LineTransformers" /> in sync.
    /// </summary>
    private void InstallHighlighter ()
    {
        // Remove old colorizer if present.
        if (_highlightingColorizer is not null)
        {
            LineTransformers.Remove (_highlightingColorizer);
            _highlightingColorizer = null;
        }

        _highlighter?.Dispose ();
        _highlighter = null;

        if (HighlightingDefinition is null || _document is null)
        {
            return;
        }

        _highlighter = new DocumentHighlighter (_document, HighlightingDefinition);
        Attribute normal = HasFocus ? GetAttributeForRole (VisualRole.Normal) : Attribute.Default;
        _highlightingColorizer = new HighlightingColorizer (
            _highlighter,
            normal,
            role => GetScheme ().TryGetExplicitlySetAttributeForRole (role, out Attribute? explicitAttr)
                ? explicitAttr
                : null);
        LineTransformers.Insert (0, _highlightingColorizer);
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
        var insertedText = e.InsertedText.Text;
        var insertedNewlines = insertedText.Count (c => c == '\n');
        var removedText = e.RemovedText.Text;
        var removedNewlines = removedText.Count (c => c == '\n');
        var lineDelta = insertedNewlines - removedNewlines;

        // Net character shift. Cached visual lines store *absolute* element offsets, so a
        // same-line-count edit upstream (no newline added/removed) still leaves every
        // downstream cached line stale even though its line *number* is unchanged.
        var offsetDelta = (insertedText.Length - removedText.Length);

        RekeyCache (_defaultVisualLineCache, threshold, lineDelta, removedNewlines, offsetDelta);
        RekeyCache (_drawVisualLineCache, threshold, lineDelta, removedNewlines, offsetDelta);

        static void RekeyCache<TValue> (Dictionary<int, TValue> cache, int threshold, int lineDelta,
            int removedNewlines, int offsetDelta)
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
                    if (lineDelta != 0)
                    {
                        // Line numbers shifted — remove old key, re-add with shifted key.
                        (toRemove ??= []).Add (kvp.Key);
                        (toRekey ??= []).Add (kvp);
                    }
                    else if (offsetDelta != 0)
                    {
                        // No newline change, but the edit shifted absolute offsets. The cached
                        // visual line for this downstream line carries stale absolute element
                        // offsets — drop it (correctness > a few cache hits). This is the defect
                        // the "Tab twice with spaces" multi-caret scenario exposes.
                        (toRemove ??= []).Add (kvp.Key);
                    }

                    // else: offsetDelta == 0 — pure in-place rewrite, downstream entries valid.
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

    private void UpdateGutterWidth ()
    {
        Thickness thickness = Padding.Thickness;
        var left = _gutterOptions != GutterOptions.None && _document is not null ? GetGutterWidth () : 0;

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
                _gutter.SyncLayout ();
            }
            else
            {
                _gutter.Width = left;
                _gutter.SyncLayout ();
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

    private int GetGutterWidth ()
    {
        var width = 0;

        if (_gutterOptions.HasFlag (GutterOptions.LineNumbers))
        {
            var lineCount = Math.Max (1, _document?.LineCount ?? 1);
            width = lineCount.ToString ().Length + 1;
        }

        // Add 2 columns for fold indicator when folding is active.
        if (_gutterOptions.HasFlag (GutterOptions.Folding) && FoldingManager is not null)
        {
            width += 2;
        }

        return width;
    }

    private int GetCaretColumn ()
    {
        return GetVisualColumnForOffset (CaretOffset);
    }

    /// <summary>
    ///     Returns the visual (cell) column of an arbitrary document offset, accounting for tabs,
    ///     double-width graphemes, and word-wrap segments. Single-caret and multi-caret vertical
    ///     placement share this so the multi-caret path never re-derives column geometry.
    /// </summary>
    private int GetVisualColumnForOffset (int caretOffset)
    {
        DocumentLine? line = _document?.GetLineByOffset (caretOffset);

        if (line is null)
        {
            return 0;
        }

        if (!WordWrap)
        {
            return GetOrBuildDefaultVisualLine (line).GetVisualColumn (caretOffset - line.Offset);
        }

        // In word-wrap mode, the column is relative to the start of the wrap segment.
        var offsetInLine = caretOffset - line.Offset;
        var text = _document!.GetText (line);
        IReadOnlyList<WrapSegment> segments =
            WordWrapStrategy.ComputeSegments (text, GetWrapColumn (), IndentationSize);

        // Find which segment the caret falls in.
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            if (offsetInLine >= segments[i].StartOffset)
            {
                var localOffset = offsetInLine - segments[i].StartOffset;
                var segText = text.Substring (segments[i].StartOffset, segments[i].Length);

                return ComputeVisualColumnDirect (segText, localOffset);
            }
        }

        return GetOrBuildDefaultVisualLine (line).GetVisualColumn (offsetInLine);
    }

    private int GetCaretLineIndex ()
    {
        return _document?.GetLineByOffset (CaretOffset).LineNumber - 1 ?? 0;
    }

    /// <summary>
    ///     Returns the caret's position as an index into the visible-line list (i.e. the coordinate
    ///     system used by <c>Viewport.Y</c>). Falls back to <see cref="GetCaretLineIndex" /> when
    ///     no folding is active.
    /// </summary>
    private int GetCaretVisibleLineIndex ()
    {
        if (WordWrap)
        {
            return GetCaretWrapRow ();
        }

        var docLineNumber = _document?.GetLineByOffset (CaretOffset).LineNumber ?? 1;
        List<int> visible = GetVisibleLineNumbers ();
        var idx = visible.IndexOf (docLineNumber);

        return idx >= 0 ? idx : GetCaretLineIndex ();
    }

    /// <summary>
    ///     Moves the caret <paramref name="delta" /> lines, preserving the sticky virtual column when
    ///     traversing shorter lines (i.e. snap back to the original column on the next long-enough line).
    /// </summary>
    private void MoveCaretVertically (int delta)
    {
        if (WordWrap)
        {
            MoveCaretVerticallyWrapped (delta);

            return;
        }

        var targetLine = Math.Clamp (GetCaretLineIndex () + delta, 0, _document!.LineCount - 1);
        DocumentLine line = _document!.GetLineByNumber (targetLine + 1);
        var targetCol = GetOrBuildDefaultVisualLine (line).GetRelativeOffset (_virtualCaretColumn);

        // resetVirtualColumn: false keeps the sticky column intact across vertical moves.
        SetCaretOffset (line.Offset + targetCol, false);
    }

    private void MoveCaretVerticallyWrapped (int delta)
    {
        List<WrapMapEntry> map = GetWrapMap ();
        var currentRow = GetCaretWrapRow ();
        var targetRow = Math.Clamp (currentRow + delta, 0, map.Count - 1);

        if (targetRow == currentRow)
        {
            return;
        }

        WrapMapEntry entry = map[targetRow];
        DocumentLine line = _document!.GetLineByNumber (entry.LineNumber);
        var text = _document.GetText (line);
        IReadOnlyList<WrapSegment> segments =
            WordWrapStrategy.ComputeSegments (text, GetWrapColumn (), IndentationSize);
        WrapSegment seg = segments[entry.SegmentIndex];

        // Resolve the virtual column within this segment.
        var segText = text.Substring (seg.StartOffset, seg.Length);
        var localOffset = ComputeRelativeOffsetDirect (segText, _virtualCaretColumn);

        SetCaretOffset (line.Offset + seg.StartOffset + localOffset, false);
    }

    /// <summary>
    ///     Resolves the document offset <paramref name="delta" /> visual rows from
    ///     <paramref name="startOffset" /> at the sticky <paramref name="targetVisualColumn" />,
    ///     using the same wrap-map / visual-line primitives as single-caret vertical movement.
    ///     Returns <see langword="false" /> (a no-op for the caller) when the move would cross the
    ///     top or bottom document bound.
    /// </summary>
    private bool TryGetVerticalOffset (int startOffset, int delta, int targetVisualColumn, out int targetOffset)
    {
        targetOffset = startOffset;

        if (_document is null)
        {
            return false;
        }

        if (WordWrap)
        {
            List<WrapMapEntry> map = GetWrapMap ();
            var targetRow = GetWrapRowForOffset (startOffset) + delta;

            if (targetRow < 0 || targetRow >= map.Count)
            {
                return false;
            }

            WrapMapEntry entry = map[targetRow];
            DocumentLine wrapLine = _document.GetLineByNumber (entry.LineNumber);
            var wrapText = _document.GetText (wrapLine);
            IReadOnlyList<WrapSegment> wrapSegments =
                WordWrapStrategy.ComputeSegments (wrapText, GetWrapColumn (), IndentationSize);
            WrapSegment seg = wrapSegments[entry.SegmentIndex];
            var segText = wrapText.Substring (seg.StartOffset, seg.Length);
            var localOffset = ComputeRelativeOffsetDirect (segText, targetVisualColumn);
            targetOffset = wrapLine.Offset + seg.StartOffset + localOffset;

            return true;
        }

        var targetLineIndex = (_document.GetLineByOffset (startOffset).LineNumber - 1) + delta;

        if (targetLineIndex < 0 || targetLineIndex > _document.LineCount - 1)
        {
            return false;
        }

        DocumentLine line = _document.GetLineByNumber (targetLineIndex + 1);
        targetOffset = line.Offset + GetOrBuildDefaultVisualLine (line).GetRelativeOffset (targetVisualColumn);

        return true;
    }

    /// <summary>Returns the visual row in the wrap map for the current caret position.</summary>
    private int GetCaretWrapRow ()
    {
        return GetWrapRowForOffset (CaretOffset);
    }

    /// <summary>Returns the visual row in the wrap map for an arbitrary document offset.</summary>
    private int GetWrapRowForOffset (int caretOffset)
    {
        if (_document is null)
        {
            return 0;
        }

        DocumentLine line = _document.GetLineByOffset (caretOffset);
        var offsetInLine = caretOffset - line.Offset;
        var text = _document.GetText (line);
        IReadOnlyList<WrapSegment> segments =
            WordWrapStrategy.ComputeSegments (text, GetWrapColumn (), IndentationSize);

        // Determine which segment the caret is in.
        var segIndex = 0;

        for (var i = segments.Count - 1; i >= 0; i--)
        {
            if (offsetInLine >= segments[i].StartOffset)
            {
                segIndex = i;

                break;
            }
        }

        // Find matching row in the wrap map.
        List<WrapMapEntry> map = GetWrapMap ();

        for (var row = 0; row < map.Count; row++)
        {
            if (map[row].LineNumber == line.LineNumber && map[row].SegmentIndex == segIndex)
            {
                return row;
            }
        }

        return 0;
    }

    private int GetCaretOffset ()
    {
        return _caretAnchor is not { IsDeleted: false } anchor ? _lastKnownCaretOffset : anchor.Offset;
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

        var caretLine = GetCaretVisibleLineIndex ();
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

        if (!WordWrap)
        {
            if (caretCol < newX)
            {
                newX = caretCol;
            }
            else if (caretCol >= newX + viewport.Width)
            {
                newX = caretCol - viewport.Width + 1;
            }
        }
        else
        {
            newX = 0;
        }

        if (newX != viewport.X || newY != viewport.Y)
        {
            Viewport = viewport with { X = newX, Y = newY };
        }
    }

    /// <summary>
    ///     Computes the visual column for a character offset within a text segment without
    ///     allocating a <see cref="TextDocument" />. Walks graphemes and accumulates widths.
    /// </summary>
    private int ComputeVisualColumnDirect (string segmentText, int targetOffset)
    {
        var visualCol = 0;
        var logicalCol = 0;

        foreach (var grapheme in GraphemeHelper.GetGraphemes (segmentText))
        {
            if (logicalCol >= targetOffset)
            {
                break;
            }

            if (grapheme == "\t")
            {
                var remainder = visualCol % IndentationSize;
                visualCol += remainder == 0 ? IndentationSize : IndentationSize - remainder;
            }
            else
            {
                visualCol += Math.Max (0, grapheme.GetColumns ());
            }

            logicalCol += grapheme.Length;
        }

        return visualCol;
    }

    /// <summary>
    ///     Computes the character offset closest to a target visual column within a text segment
    ///     without allocating a <see cref="TextDocument" />. Inverse of
    ///     <see cref="ComputeVisualColumnDirect" />.
    /// </summary>
    private int ComputeRelativeOffsetDirect (string segmentText, int targetVisualColumn)
    {
        var visualCol = 0;
        var logicalCol = 0;

        foreach (var grapheme in GraphemeHelper.GetGraphemes (segmentText))
        {
            int width;

            if (grapheme == "\t")
            {
                var remainder = visualCol % IndentationSize;
                width = remainder == 0 ? IndentationSize : IndentationSize - remainder;
            }
            else
            {
                width = Math.Max (0, grapheme.GetColumns ());
            }

            if (visualCol + width > targetVisualColumn)
            {
                return logicalCol;
            }

            visualCol += width;
            logicalCol += grapheme.Length;
        }

        return logicalCol;
    }

    /// <summary>
    ///     Returns the wrap map, building it lazily. Each entry corresponds to one visual row
    ///     in the document when word wrap is active. Automatically rebuilds if the wrap column
    ///     has changed (viewport resize) or visible lines have been invalidated (fold toggle).
    /// </summary>
    private List<WrapMapEntry> GetWrapMap ()
    {
        var wrapColumn = GetWrapColumn ();

        if (_wrapMap is not null && _wrapMapColumn == wrapColumn)
        {
            return _wrapMap;
        }

        _wrapMap = [];
        _wrapMapColumn = wrapColumn;

        if (_document is null)
        {
            return _wrapMap;
        }

        List<int> visibleLineNumbers = GetVisibleLineNumbers ();

        foreach (var lineNumber in visibleLineNumbers)
        {
            DocumentLine line = _document.GetLineByNumber (lineNumber);
            var text = _document.GetText (line);
            IReadOnlyList<WrapSegment> segments =
                WordWrapStrategy.ComputeSegments (text, wrapColumn, IndentationSize);

            for (var i = 0; i < segments.Count; i++)
            {
                _wrapMap.Add (new WrapMapEntry (lineNumber, i, segments[i].StartOffset));
            }
        }

        return _wrapMap;
    }

    /// <summary>Returns the effective wrap column (viewport width).</summary>
    private int GetWrapColumn ()
    {
        Rectangle viewport = Viewport;

        return viewport.Width > 0 ? viewport.Width : 80;
    }

    private readonly record struct DrawCacheEntry (CellVisualLine Line, Attribute Normal, Attribute Selected);

    /// <summary>Maps a visual row (in the wrap map) to a document line and segment within that line.</summary>
    private readonly record struct WrapMapEntry (int LineNumber, int SegmentIndex, int SegmentStartOffset);
}

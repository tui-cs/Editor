using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views.Rendering;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <summary>Cached visible-line mapping; cleared when folds change or the document changes.</summary>
    private List<int>? _cachedVisibleLineNumbers;

    private ISyntaxHighlighter? _highlighterPreparedInstance;

    // Syntax highlighter state optimization: tracks how far we've prepared so incremental
    // scrolling doesn't re-highlight from line 0 every frame.
    private int _highlighterPreparedUpToLine = -1;

    /// <inheritdoc />
    protected override bool OnDrawingContent (DrawContext? context)
    {
        if (_document is null)
        {
            return true;
        }

        Rectangle viewport = Viewport;
        Attribute normal = GetAttributeForRole (VisualRole.Normal);
        Attribute selected = GetAttributeForRole (VisualRole.Active);

        FillViewportBackground (viewport, normal);
        DrawVisibleLines (viewport, normal, selected);
        SetAttribute (normal);
        UpdateCursor ();

        return true;
    }

    /// <summary>
    ///     When <see cref="UseThemeBackground" /> is <see langword="true" /> and a syntax highlighter
    ///     provides a <see cref="ISyntaxHighlighter.DefaultBackground" />, fills the viewport with
    ///     that background so empty cells match per-token backgrounds.
    /// </summary>
    private void FillViewportBackground (Rectangle viewport, Attribute normal)
    {
        if (!UseThemeBackground)
        {
            return;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        Color? themeBg = SyntaxHighlighter?.DefaultBackground;
#pragma warning restore CS0618 // Type or member is obsolete

        if (themeBg is not { } bg)
        {
            return;
        }

        Attribute fillAttr = new (normal.Foreground, bg);
        SetAttribute (fillAttr);

        var spaces = new string (' ', viewport.Width);

        for (var row = 0; row < viewport.Height; row++)
        {
            AddStr (0, row, spaces);
        }
    }

    private void DrawVisibleLines (Rectangle viewport, Attribute normal, Attribute selected)
    {
        // The CS0618 here is the API's purpose: SyntaxHighlighter is [Obsolete] to warn
        // external callers that this is a stopgap (issue #32). The editor itself still has to
        // honor the property until Phase 6 lifts AvaloniaEdit's Highlighting/ pipeline (#28).
#pragma warning disable CS0618 // Type or member is obsolete
        ISyntaxHighlighter? syntaxHighlighter = SyntaxHighlighter;
#pragma warning restore CS0618 // Type or member is obsolete

        var hasSelection = HasSelection;
        var selStart = hasSelection ? SelectionStart : 0;
        var selEnd = hasSelection ? SelectionEnd : 0;
        var visibleStart = viewport.X;
        var visibleEnd = viewport.X + viewport.Width;

        // Build a mapping from viewport row → document line number (1-based),
        // skipping lines hidden by collapsed folds.
        List<int> visibleLineNumbers = GetVisibleLineNumbers ();

        // Prime the highlighter from the first visible *document* line, not the viewport row index,
        // so that folded regions above the viewport don't leave the highlighter in stale state.
        var firstVisibleIndex = viewport.Y;
        var firstDocLine = firstVisibleIndex >= 0 && firstVisibleIndex < visibleLineNumbers.Count
            ? visibleLineNumbers[firstVisibleIndex] - 1
            : viewport.Y;
        PrepareSyntaxHighlighter (syntaxHighlighter, firstDocLine);

        for (var row = 0; row < viewport.Height; row++)
        {
            var visibleIndex = viewport.Y + row;

            if (visibleIndex < 0 || visibleIndex >= visibleLineNumbers.Count)
            {
                break;
            }

            var lineNumber = visibleLineNumbers[visibleIndex];
            DocumentLine line = _document!.GetLineByNumber (lineNumber);
#pragma warning disable CS0618 // Type or member is obsolete — see PrepareSyntaxHighlighter.
            IReadOnlyList<StyledSegment>? segments =
                syntaxHighlighter?.Highlight (_document.GetText (line), SyntaxLanguage);
#pragma warning restore CS0618 // Type or member is obsolete

            DrawVisualLine (row, line, visibleStart, visibleEnd, segments, normal, selected, selStart, selEnd);
        }
    }

    /// <summary>
    ///     Returns a list of 1-based document line numbers that are visible (not hidden by folds),
    ///     in order. Cached until folds change.
    /// </summary>
    internal List<int> GetVisibleLineNumbers ()
    {
        if (_cachedVisibleLineNumbers is not null)
        {
            return _cachedVisibleLineNumbers;
        }

        List<int> result = new ();

        if (_document is null)
        {
            _cachedVisibleLineNumbers = result;

            return result;
        }

        FoldingManager? fm = FoldingManager;
        var lineNumber = 1;

        while (lineNumber <= _document.LineCount)
        {
            result.Add (lineNumber);

            if (fm is not null)
            {
                // If there's a folded section starting on this line, skip its hidden lines.
                FoldingSection? fold = fm.GetFoldingAtLine (lineNumber);

                if (fold is { IsFolded: true })
                {
                    DocumentLine endLine =
                        fm.Document.GetLineByOffset (Math.Clamp (fold.EndOffset, 0, fm.Document.TextLength));

                    if (endLine.LineNumber > lineNumber)
                    {
                        lineNumber = endLine.LineNumber + 1;

                        continue;
                    }
                }
            }

            lineNumber++;
        }

        _cachedVisibleLineNumbers = result;

        return result;
    }

    private void PrepareSyntaxHighlighter (ISyntaxHighlighter? syntaxHighlighter, int firstVisibleLineIndex)
    {
        if (syntaxHighlighter is null || _document is null)
        {
            return;
        }

        // If the highlighter instance changed or viewport scrolled backward, reset from scratch.
        if (!ReferenceEquals (syntaxHighlighter, _highlighterPreparedInstance)
            || firstVisibleLineIndex < _highlighterPreparedUpToLine)
        {
            syntaxHighlighter.ResetState ();
            _highlighterPreparedInstance = syntaxHighlighter;
            _highlighterPreparedUpToLine = 0;
        }

        // Incrementally highlight from where we left off to the first visible line.
        for (var lineIndex = _highlighterPreparedUpToLine;
             lineIndex < firstVisibleLineIndex && lineIndex < _document.LineCount;
             lineIndex++)
        {
            DocumentLine line = _document.GetLineByNumber (lineIndex + 1);
#pragma warning disable CS0618 // Type or member is obsolete — see note in OnDrawingContent.
            syntaxHighlighter.Highlight (_document.GetText (line), SyntaxLanguage);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        _highlighterPreparedUpToLine = firstVisibleLineIndex;
    }

    private void DrawVisualLine (
        int row,
        DocumentLine line,
        int visibleStart,
        int visibleEnd,
        IReadOnlyList<StyledSegment>? segments,
        Attribute normal,
        Attribute selected,
        int selStart,
        int selEnd)
    {
        // GetOrBuildDrawVisualLine caches when segments == null && no selection && no transformers,
        // i.e. plain-text scrolling without a highlighter. The caret-path cache is separate so the
        // two don't thrash each other's entries (they use different attribute sets).
        CellVisualLine visualLine = GetOrBuildDrawVisualLine (line, segments, normal, selected, selStart, selEnd);

        foreach (IBackgroundRenderer renderer in BackgroundRenderers)
        {
            renderer.Draw (this, visualLine, row, Viewport);
        }

        foreach (CellVisualLineElement element in visualLine.Elements)
        {
            // Elements are ordered by visual column. Once we pass the visible end,
            // all remaining elements are off-screen — skip them entirely.
            if (element.VisualColumn >= visibleEnd)
            {
                break;
            }

            if (element.VisualEndColumn <= visibleStart)
            {
                continue;
            }

            element.Draw (this, 0, row, visibleStart, visibleEnd);
        }
    }

    private void UpdateCursor ()
    {
        if (!HasFocus || _document is null)
        {
            Cursor = new Cursor ();

            return;
        }

        Rectangle viewport = Viewport;
        var caretLine = GetCaretVisibleLineIndex ();
        var caretCol = GetCaretColumn ();
        var row = caretLine - viewport.Y;
        var col = caretCol - viewport.X;

        if (row < 0 || row >= viewport.Height || col < 0 || col >= viewport.Width)
        {
            Cursor = new Cursor ();

            return;
        }

        Point screen = ViewportToScreen (new Point (col, row));
        Cursor = new Cursor { Position = screen, Style = CursorStyle.BlinkingBar };
    }
}

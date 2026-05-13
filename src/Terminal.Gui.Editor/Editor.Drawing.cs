using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Highlighting;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Editor.Rendering;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    /// <summary>Cached visible-line mapping; cleared when folds change or the document changes.</summary>
    private List<int>? _cachedVisibleLineNumbers;

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

        // Ensure the colorizer sees the current attribute (scheme may have changed since install).
        EnsureColorizerAttribute (normal);

        FillViewportBackground (viewport, normal);
        DrawVisibleLines (viewport, normal, selected);
        SetAttribute (normal);
        UpdateCursor ();

        return true;
    }

    /// <summary>
    ///     When <see cref="UseThemeBackground" /> is <see langword="true" /> and a highlighting
    ///     definition has a default background color, fills the viewport with that background
    ///     so empty cells match per-token backgrounds.
    /// </summary>
    private void FillViewportBackground (Rectangle viewport, Attribute normal)
    {
        if (!UseThemeBackground)
        {
            return;
        }

        Color? themeBg = _highlighter?.DefaultTextColor?.Background?.Color;

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
        var hasSelection = HasSelection;
        var selStart = hasSelection ? SelectionStart : 0;
        var selEnd = hasSelection ? SelectionEnd : 0;
        var visibleStart = viewport.X;
        var visibleEnd = viewport.X + viewport.Width;

        if (WordWrap)
        {
            DrawWrappedLines (viewport, normal, selected, selStart, selEnd);

            return;
        }

        // Build a mapping from viewport row → document line number (1-based),
        // skipping lines hidden by collapsed folds.
        List<int> visibleLineNumbers = GetVisibleLineNumbers ();

        for (var row = 0; row < viewport.Height; row++)
        {
            var visibleIndex = viewport.Y + row;

            if (visibleIndex < 0 || visibleIndex >= visibleLineNumbers.Count)
            {
                break;
            }

            var lineNumber = visibleLineNumbers[visibleIndex];
            DocumentLine line = _document!.GetLineByNumber (lineNumber);

            DrawVisualLine (row, line, visibleStart, visibleEnd, null, normal, selected, selStart, selEnd);
        }
    }

    private void DrawWrappedLines (Rectangle viewport, Attribute normal, Attribute selected, int selStart, int selEnd)
    {
        List<WrapMapEntry> map = GetWrapMap ();
        var wrapColumn = GetWrapColumn ();

        for (var row = 0; row < viewport.Height; row++)
        {
            var visibleIndex = viewport.Y + row;

            if (visibleIndex < 0 || visibleIndex >= map.Count)
            {
                break;
            }

            WrapMapEntry entry = map[visibleIndex];
            DocumentLine line = _document!.GetLineByNumber (entry.LineNumber);
            var text = _document.GetText (line);
            IReadOnlyList<WrapSegment> segments =
                WordWrapStrategy.ComputeSegments (text, wrapColumn, IndentationSize);
            WrapSegment seg = segments[entry.SegmentIndex];

            var segText = text.Substring (seg.StartOffset, seg.Length);

            CellVisualLine visualLine = BuildWrappedSegmentVisualLine (
                line, seg.StartOffset, segText, normal, selected, selStart, selEnd);

            foreach (IBackgroundRenderer renderer in BackgroundRenderers)
            {
                renderer.Draw (this, visualLine, row, viewport);
            }

            foreach (CellVisualLineElement element in visualLine.Elements)
            {
                if (element.VisualColumn >= viewport.Width)
                {
                    break;
                }

                element.Draw (this, 0, row, 0, viewport.Width);
            }
        }
    }

    private CellVisualLine BuildWrappedSegmentVisualLine (
        DocumentLine documentLine,
        int segmentStartOffset,
        string segmentText,
        Attribute normal,
        Attribute selected,
        int selStart,
        int selEnd)
    {
        CellVisualLine visualLine = new (documentLine);
        var visualColumn = 0;

        for (var i = 0; i < segmentText.Length; i++)
        {
            var documentOffset = documentLine.Offset + segmentStartOffset + i;
            Attribute attribute = normal;

            if (selStart < selEnd && documentOffset >= selStart && documentOffset < selEnd)
            {
                attribute = selected;
            }

            if (segmentText[i] == '\t')
            {
                var width = VisualLineBuilder.GetTabExpansionWidth (visualColumn, IndentationSize);
                visualLine.AddElement (
                    new TabElement (documentOffset, visualColumn, width, ShowTabs, attribute));
                visualColumn += width;
            }
            else if (segmentText[i] <= 127)
            {
                TextRunElement element = new (documentOffset, 1, visualColumn, segmentText.Substring (i, 1), attribute);
                visualLine.AddElement (element);
                visualColumn += 1;
            }
            else
            {
                // Handle multi-byte graphemes — fall through to grapheme path for remainder.
                var remaining = segmentText[i..];

                foreach (var grapheme in GraphemeHelper.GetGraphemes (remaining))
                {
                    var gDocOffset = documentLine.Offset + segmentStartOffset + i;

                    Attribute gAttribute = normal;

                    if (selStart < selEnd && gDocOffset >= selStart && gDocOffset < selEnd)
                    {
                        gAttribute = selected;
                    }

                    TextRunElement gElement = new (gDocOffset, grapheme.Length, visualColumn, grapheme, gAttribute);
                    visualLine.AddElement (gElement);
                    visualColumn += gElement.VisualLength;
                    i += grapheme.Length;
                }

                i--; // Compensate for the for-loop increment.

                break;
            }
        }

        // Apply transformers.
        foreach (IVisualLineTransformer transformer in LineTransformers)
        {
            transformer.Transform (visualLine);
        }

        return visualLine;
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
                // If there are folded sections starting on this line, skip past the deepest one.
                var maxEndLine = lineNumber;

                foreach (FoldingSection fs in fm.AllFoldings)
                {
                    if (!fs.IsFolded)
                    {
                        continue;
                    }

                    DocumentLine startLine =
                        fm.Document.GetLineByOffset (Math.Clamp (fs.StartOffset, 0, fm.Document.TextLength));

                    if (startLine.LineNumber != lineNumber)
                    {
                        continue;
                    }

                    DocumentLine endLine =
                        fm.Document.GetLineByOffset (Math.Clamp (fs.EndOffset, 0, fm.Document.TextLength));

                    if (endLine.LineNumber > maxEndLine)
                    {
                        maxEndLine = endLine.LineNumber;
                    }
                }

                if (maxEndLine > lineNumber)
                {
                    lineNumber = maxEndLine + 1;

                    continue;
                }
            }

            lineNumber++;
        }

        _cachedVisibleLineNumbers = result;

        return result;
    }

    /// <summary>
    ///     Rebuilds the <see cref="HighlightingColorizer" /> if the editor's normal attribute
    ///     has changed (e.g. after a scheme swap or focus change). Keeps the same underlying
    ///     <see cref="DocumentHighlighter" />.
    /// </summary>
    private void EnsureColorizerAttribute (Attribute normal)
    {
        if (_highlightingColorizer is null)
        {
            return;
        }

        HighlightingColorizer replacement = _highlightingColorizer.WithDefaultAttribute (normal, UseThemeBackground);

        if (ReferenceEquals (replacement, _highlightingColorizer))
        {
            return;
        }

        var index = LineTransformers.IndexOf (_highlightingColorizer);

        if (index >= 0)
        {
            LineTransformers[index] = replacement;
        }
        else
        {
            LineTransformers.Insert (0, replacement);
        }

        _highlightingColorizer = replacement;
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

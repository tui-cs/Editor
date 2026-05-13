using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;

namespace Terminal.Gui.Views.Rendering;

/// <summary>
///     An <see cref="IVisualLineTransformer" /> that replaces the content of collapsed
///     <see cref="FoldingSection" />s with a <see cref="FoldingMarkerElement" /> rendering <c>"⋯"</c>.
/// </summary>
public sealed class FoldingTransformer : IVisualLineTransformer
{
    private readonly FoldingManager _foldingManager;

    /// <summary>Creates a new <see cref="FoldingTransformer" /> for the given <paramref name="foldingManager" />.</summary>
    public FoldingTransformer (FoldingManager foldingManager)
    {
        _foldingManager = foldingManager ?? throw new ArgumentNullException (nameof (foldingManager));
    }

    /// <inheritdoc />
    public void Transform (CellVisualLine line)
    {
        DocumentLine docLine = line.DocumentLine;
        var lineStartOffset = docLine.Offset;
        var lineEndOffset = docLine.Offset + docLine.Length;

        // Find folded sections that start on this line.
        foreach (FoldingSection fs in _foldingManager.GetFoldingsContaining (lineStartOffset))
        {
            if (!fs.IsFolded)
            {
                continue;
            }

            DocumentLine startLine = _foldingManager.Document.GetLineByOffset (fs.StartOffset);

            if (startLine.LineNumber != docLine.LineNumber)
            {
                continue;
            }

            ReplaceFoldedRange (line, fs);
        }

        // Also check foldings that start within the line but not at its beginning offset.
        foreach (FoldingSection fs in _foldingManager.AllFoldings)
        {
            if (!fs.IsFolded)
            {
                continue;
            }

            if (fs.StartOffset < lineStartOffset || fs.StartOffset >= lineEndOffset)
            {
                continue;
            }

            DocumentLine startLine = _foldingManager.Document.GetLineByOffset (fs.StartOffset);

            if (startLine.LineNumber != docLine.LineNumber)
            {
                continue;
            }

            ReplaceFoldedRange (line, fs);
        }
    }

    private static void ReplaceFoldedRange (CellVisualLine line, FoldingSection fs)
    {
        DocumentLine docLine = line.DocumentLine;
        var foldStartInLine = fs.StartOffset - docLine.Offset;
        var foldEndInLine = fs.EndOffset - docLine.Offset;

        // Find the visual column where the fold starts.
        var foldVisualColumn = 0;
        List<CellVisualLineElement> elementsToKeep = new ();
        var markerInserted = false;

        foreach (CellVisualLineElement element in line.Elements)
        {
            var elementRelStart = element.DocumentOffset - docLine.Offset;

            if (elementRelStart < foldStartInLine)
            {
                elementsToKeep.Add (element);
                foldVisualColumn = element.VisualEndColumn;
            }
            else if (!markerInserted)
            {
                // Insert the fold marker at this position.
                var title = fs.Title ?? "⋯";
                FoldingMarkerElement marker = new (
                    fs.StartOffset,
                    fs.EndOffset - fs.StartOffset,
                    foldVisualColumn,
                    element.Attribute,
                    title);
                elementsToKeep.Add (marker);
                markerInserted = true;

                // Skip all remaining elements — they're hidden by the fold.
                break;
            }
        }

        if (!markerInserted && line.Elements.Count > 0)
        {
            // Edge case: fold starts at the end of all elements.
            CellVisualLineElement lastElement = line.Elements[^1];
            var title = fs.Title ?? "⋯";
            FoldingMarkerElement marker = new (
                fs.StartOffset,
                fs.EndOffset - fs.StartOffset,
                lastElement.VisualEndColumn,
                lastElement.Attribute,
                title);
            elementsToKeep.Add (marker);
        }

        // Replace the line's elements with the kept elements.
        line.ReplaceElements (elementsToKeep);
    }
}

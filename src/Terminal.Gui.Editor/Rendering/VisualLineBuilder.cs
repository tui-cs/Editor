using Terminal.Gui.Drawing;
using Terminal.Gui.Text.Document;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Views.Rendering;

/// <summary>Builds terminal-cell visual lines from document lines.</summary>
public sealed class VisualLineBuilder
{
    public CellVisualLine Build (DocumentLine documentLine, VisualLineBuildContext context)
    {
        var text = context.Document.GetText (documentLine);
        CellVisualLine visualLine = new (documentLine);

        if (IsAsciiOnly (text))
        {
            BuildAsciiFastPath (documentLine, context, text, visualLine);
        }
        else
        {
            BuildGraphemePath (documentLine, context, text, visualLine);
        }

        foreach (IVisualLineTransformer transformer in context.LineTransformers)
        {
            transformer.Transform (visualLine);
        }

        return visualLine;
    }

    /// <summary>
    ///     Fast path for pure-ASCII lines. Avoids <c>GraphemeHelper.GetGraphemes</c>
    ///     allocation — each byte is one grapheme, one column (tabs expand as usual).
    /// </summary>
    private static void BuildAsciiFastPath (
        DocumentLine documentLine,
        VisualLineBuildContext context,
        string text,
        CellVisualLine visualLine)
    {
        var segmentIndex = 0;
        var segmentEnd = GetSegmentEnd (context.StyledSegments, segmentIndex);
        var visualColumn = 0;

        for (var i = 0; i < text.Length; i++)
        {
            while (context.StyledSegments is { Count: > 0 }
                   && i >= segmentEnd
                   && segmentIndex + 1 < context.StyledSegments.Count)
            {
                segmentIndex++;
                segmentEnd += context.StyledSegments[segmentIndex].Text.Length;
            }

            Attribute attribute = GetAttribute (context, segmentIndex);
            var documentOffset = documentLine.Offset + i;

            if (context.HasSelection
                && documentOffset < context.SelectionEnd
                && documentOffset + 1 > context.SelectionStart)
            {
                attribute = context.SelectedAttribute;
            }

            if (text[i] == '\t')
            {
                var width = GetTabExpansionWidth (visualColumn, context.IndentationSize);
                visualLine.AddElement (
                    new TabElement (documentOffset, visualColumn, width, context.ShowTabs, attribute));
                visualColumn += width;
            }
            else
            {
                TextRunElement element = new (documentOffset, 1, visualColumn, text.Substring (i, 1), attribute);
                visualLine.AddElement (element);
                visualColumn += 1;
            }
        }
    }

    private static void BuildGraphemePath (
        DocumentLine documentLine,
        VisualLineBuildContext context,
        string text,
        CellVisualLine visualLine)
    {
        var logicalColumn = 0;
        var visualColumn = 0;
        var segmentIndex = 0;
        var segmentEnd = GetSegmentEnd (context.StyledSegments, segmentIndex);

        foreach (var grapheme in GraphemeHelper.GetGraphemes (text))
        {
            while (context.StyledSegments is { Count: > 0 }
                   && logicalColumn >= segmentEnd
                   && segmentIndex + 1 < context.StyledSegments.Count)
            {
                segmentIndex++;
                segmentEnd += context.StyledSegments[segmentIndex].Text.Length;
            }

            Attribute attribute = GetAttribute (context, segmentIndex);
            var documentOffset = documentLine.Offset + logicalColumn;
            var documentLength = grapheme.Length;

            if (context.HasSelection
                && documentOffset < context.SelectionEnd
                && documentOffset + documentLength > context.SelectionStart)
            {
                attribute = context.SelectedAttribute;
            }

            if (grapheme == "\t")
            {
                var width = GetTabExpansionWidth (visualColumn, context.IndentationSize);
                visualLine.AddElement (
                    new TabElement (documentOffset, visualColumn, width, context.ShowTabs, attribute));
                visualColumn += width;
            }
            else
            {
                TextRunElement element = new (documentOffset, documentLength, visualColumn, grapheme, attribute);
                visualLine.AddElement (element);
                visualColumn += element.VisualLength;
            }

            logicalColumn += documentLength;
        }
    }

    /// <summary>Checks if all characters are ASCII (no multi-byte graphemes, no surrogates).</summary>
    private static bool IsAsciiOnly (string text)
    {
        foreach (var c in text)
        {
            if (c > 127)
            {
                return false;
            }
        }

        return true;
    }

    public static int GetTabExpansionWidth (int visualColumn, int indentationSize)
    {
        var remainder = visualColumn % indentationSize;

        return remainder == 0 ? indentationSize : indentationSize - remainder;
    }

    private static Attribute GetAttribute (VisualLineBuildContext context, int segmentIndex)
    {
        if (context.StyledSegments is not { Count: > 0 })
        {
            return context.NormalAttribute;
        }

        Attribute segAttr = context.StyledSegments[Math.Min (segmentIndex, context.StyledSegments.Count - 1)].Attribute
                            ?? context.NormalAttribute;

        if (context.UseThemeBackground)
        {
            return new Attribute (segAttr.Foreground, context.NormalAttribute.Background);
        }

        return segAttr;
    }

    private static int GetSegmentEnd (IReadOnlyList<StyledSegment>? segments, int segmentIndex)
    {
        return segments is { Count: > 0 } ? segments[segmentIndex].Text.Length : int.MaxValue;
    }
}

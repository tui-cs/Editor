using Terminal.Gui.Drawing;
using Terminal.Gui.Highlighting;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Views.Rendering;

/// <summary>
///     An <see cref="IVisualLineTransformer" /> that applies syntax highlighting colors
///     from a <see cref="DocumentHighlighter" /> to visual-line elements.
/// </summary>
/// <remarks>
///     <para>
///         The colorizer calls <see cref="IHighlighter.HighlightLine" /> for each visible line
///         and maps the resulting <see cref="HighlightedSection" /> color ranges onto the
///         <see cref="CellVisualLineElement.Attribute" /> of each element in the
///         <see cref="CellVisualLine" />.
///     </para>
///     <para>
///         Typically added to <see cref="Editor.LineTransformers" /> automatically when
///         <see cref="Editor.HighlightingDefinition" /> is set; consumers rarely need to
///         instantiate this directly.
///     </para>
/// </remarks>
public sealed class HighlightingColorizer : IVisualLineTransformer
{
    private readonly Attribute _defaultAttribute;
    private readonly bool _useThemeBackground;

    /// <summary>Creates a new <see cref="HighlightingColorizer" />.</summary>
    /// <param name="highlighter">The document highlighter that produces per-line color information.</param>
    /// <param name="defaultAttribute">
    ///     The editor's normal attribute, used as a fallback when a highlighting color does not
    ///     specify foreground or background.
    /// </param>
    /// <param name="useThemeBackground">
    ///     When <see langword="true" />, highlighted background colors are preserved. When
    ///     <see langword="false" />, only the foreground is taken from the highlighting color
    ///     and the background falls back to <paramref name="defaultAttribute" />.
    /// </param>
    public HighlightingColorizer (IHighlighter highlighter, Attribute defaultAttribute, bool useThemeBackground)
    {
        Highlighter = highlighter ?? throw new ArgumentNullException (nameof (highlighter));
        _defaultAttribute = defaultAttribute;
        _useThemeBackground = useThemeBackground;
    }

    /// <summary>Gets the underlying highlighter.</summary>
    public IHighlighter Highlighter { get; }

    /// <inheritdoc />
    public void Transform (CellVisualLine line)
    {
        if (line.Elements.Count == 0)
        {
            return;
        }

        HighlightedLine highlightedLine;

        try
        {
            highlightedLine = Highlighter.HighlightLine (line.DocumentLine.LineNumber);
        }
        catch (HighlightingDefinitionInvalidException)
        {
            // Bad grammar — leave elements with their default attributes.
            return;
        }

        IList<HighlightedSection> sections = highlightedLine.Sections;

        if (sections.Count == 0)
        {
            return;
        }

        foreach (CellVisualLineElement element in line.Elements)
        {
            Attribute merged = ResolveAttribute (element, sections);
            element.Attribute = merged;
        }
    }

    /// <summary>Updates the default attribute (e.g. when the editor's color scheme changes).</summary>
    public HighlightingColorizer WithDefaultAttribute (Attribute defaultAttribute, bool useThemeBackground)
    {
        if (_defaultAttribute == defaultAttribute && _useThemeBackground == useThemeBackground)
        {
            return this;
        }

        return new HighlightingColorizer (Highlighter, defaultAttribute, useThemeBackground);
    }

    /// <summary>
    ///     Finds the innermost <see cref="HighlightedSection" /> that covers the element's
    ///     document range and converts its <see cref="HighlightingColor" /> to an
    ///     <see cref="Attribute" />. Sections are sorted by start offset with outer sections
    ///     before inner ones, so the last covering section is the most specific.
    /// </summary>
    private Attribute ResolveAttribute (CellVisualLineElement element, IList<HighlightedSection> sections)
    {
        HighlightingColor? bestColor = null;

        foreach (HighlightedSection section in sections)
        {
            if (section.Offset > element.DocumentOffset)
            {
                // Sections are sorted by offset. Once we pass the element, no later section
                // can cover it (unless nested, but nested sections come before their parent's end).
                // However, nested sections come *after* the outer section in the list, so we
                // can't break early — we need the innermost match.
                if (section.Offset >= element.DocumentEndOffset)
                {
                    break;
                }
            }

            var sectionEnd = section.Offset + section.Length;

            // Does this section cover the element's start offset?
            if (section.Offset <= element.DocumentOffset && sectionEnd >= element.DocumentEndOffset)
            {
                bestColor = section.Color;
            }
        }

        return ToAttribute (bestColor);
    }

    /// <summary>Converts a <see cref="HighlightingColor" /> to a Terminal.Gui <see cref="Attribute" />.</summary>
    private Attribute ToAttribute (HighlightingColor? color)
    {
        if (color is null)
        {
            return _defaultAttribute;
        }

        Color fg = color.Foreground?.Color ?? _defaultAttribute.Foreground;
        Color bg = _useThemeBackground
            ? color.Background?.Color ?? _defaultAttribute.Background
            : _defaultAttribute.Background;

        return new Attribute (fg, bg);
    }
}

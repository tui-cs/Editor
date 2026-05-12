using Terminal.Gui.Drawing;
using Terminal.Gui.Text.Document;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Views.Rendering;

/// <summary>Options and styling inputs used to build a visual line.</summary>
public sealed class VisualLineBuildContext (
    TextDocument document,
    int indentationSize,
    bool showTabs,
    Attribute normalAttribute,
    Attribute selectedAttribute,
    IReadOnlyList<StyledSegment>? styledSegments,
    int selectionStart,
    int selectionEnd,
    IEnumerable<IVisualLineTransformer> lineTransformers,
    bool useThemeBackground = true)
{
    public TextDocument Document { get; } = document;

    public int IndentationSize { get; } = indentationSize;

    public bool ShowTabs { get; } = showTabs;

    public Attribute NormalAttribute { get; } = normalAttribute;

    public Attribute SelectedAttribute { get; } = selectedAttribute;

    public IReadOnlyList<StyledSegment>? StyledSegments { get; } = styledSegments;

    public int SelectionStart { get; } = selectionStart;

    public int SelectionEnd { get; } = selectionEnd;

    public IEnumerable<IVisualLineTransformer> LineTransformers { get; } = lineTransformers;

    /// <summary>
    ///     When <see langword="true" />, styled-segment backgrounds are replaced with
    ///     <see cref="NormalAttribute" />'s background so text blends into the theme.
    /// </summary>
    public bool UseThemeBackground { get; } = useThemeBackground;

    public bool HasSelection => SelectionStart < SelectionEnd;
}

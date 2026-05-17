using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Ted;

/// <summary>
///     Renders the ted logo in box-drawing characters with a diagonal gradient.
/// </summary>
public sealed class TedLogo : View
{
    // @formatter:off
    private const string ART = """
                                ╻        ╻
                               ╺╋╸ ┏━┓ ┏━┫  
                                ┃  ┣━┛ ┃ ┃
                                ╹  ┗━╸ ┗━┛
                               """;

    // @formatter:on

    private static readonly string[] _artLines = ART.ReplaceLineEndings ("\n").Split ('\n');

    public TedLogo ()
    {
        var artWidth = _artLines.Select (line => line.Length).Prepend (0).Max ();

        Width = artWidth;
        Height = _artLines.Length;
    }

    /// <inheritdoc />
    protected override bool OnDrawingContent (DrawContext? context)
    {
        List<Color> stops =
        [
            new (0, 128, 255), // Bright Blue
            new (0, 255, 128), // Bright Green
            new (255, 255), // Bright Yellow
            new (255, 128) // Bright Orange
        ];

        List<int> steps = [10];

        Gradient gradient = new (stops, steps);

        var artHeight = 5;
        var artWidth = _artLines[0].Length;

        Dictionary<Point, Color> colorMap =
            gradient.BuildCoordinateColorMapping (artHeight, artWidth, GradientDirection.Diagonal);

        Attribute normalAttr = GetAttributeForRole (VisualRole.Normal);

        for (var row = 0; row < _artLines.Length; row++)
        {
            var line = _artLines[row];

            for (var col = 0; col < line.Length; col++)
            {
                var ch = line[col];

                if (ch == ' ')
                {
                    continue;
                }

                if (row < 5)
                {
                    Point c = new (col, row);

                    SetAttribute (colorMap.TryGetValue (c, out Color color)
                        ? new Attribute (color, normalAttr.Background)
                        : normalAttr);
                }
                else
                {
                    SetAttribute (normalAttr);
                }

                Move (col, row);
                AddStr (ch.ToString ());
            }
        }

        return true;
    }
}

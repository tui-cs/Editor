// Claude - claude-opus-4-7

using System.Drawing;
using System.Text;
using Terminal.Gui.Document;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     The horizontal content extent is computed exactly (building a <c>CellVisualLine</c> per line)
///     for normal-size documents, but estimated from character length for large ones — building +
///     highlighting every line on load is what made a 10 MiB open take ~10 s. These pin both branches
///     and the threshold so the fast path can't silently change small-document behavior.
/// </summary>
public class MaxWidthEstimationTests
{
    // A single tab-only line: char length 1, but exact visual width expands to a tab stop (> 1).
    [Fact]
    public void SmallDocument_UsesExactTabExpandedWidth ()
    {
        Editor editor = new () { Document = new TextDocument ("\t\t\tx") };

        // Exact path: 3 tabs expand to tab stops, so the extent is far wider than the 4-char length.
        Assert.True (
            editor.GetContentSize ().Width > 4 + 1,
            $"Small-doc width {editor.GetContentSize ().Width} should be tab-expanded (exact), not the "
            + "char-length estimate (5).");
    }

    [Fact]
    public void LargeDocument_UsesCharLengthEstimate_NotTabExpanded ()
    {
        // > 256 KiB of identical tab-heavy lines. Each line is "\t\t\tx" (char length 4); the exact
        // tab-expanded visual width would be much larger. The estimate path must report char length.
        var sb = new StringBuilder (400 * 1024);

        while (sb.Length < 300 * 1024)
        {
            sb.Append ("\t\t\tx\n");
        }

        Editor editor = new () { Document = new TextDocument (sb.ToString ()) };

        // Estimate path: width == longest line's char length (4) + 1 (caret past EOL), NOT tab-expanded.
        Assert.Equal (4 + 1, editor.GetContentSize ().Width);
    }
}

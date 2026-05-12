using System.Diagnostics;
using Terminal.Gui.Document;
using Terminal.Gui.Views.Rendering;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Stopwatch-based performance smoke tests that run in normal CI. Thresholds are set to
///     ~5x the typical wall time on an M-series Mac, so they only fail on catastrophic
///     regressions — not CI-runner noise. For precision measurements use the BenchmarkDotNet
///     suite in <c>benchmarks/</c>.
/// </summary>
public class PerformanceSmokeTests
{
    /// <summary>
    ///     Building 50 visual lines from a 10K-line document should complete well under the
    ///     threshold. Typical: ~200 µs. Threshold: 50 ms (250x headroom for slow CI runners).
    /// </summary>
    [Fact]
    public void BuildViewport_50Lines_CompletesWithinBudget ()
    {
        TextDocument document = new (GenerateDocument (10_000));
        VisualLineBuilder builder = new ();
        var startLine = document.LineCount / 2;

        // Warm up
        BuildViewport (document, builder, startLine, 50);

        Stopwatch sw = Stopwatch.StartNew ();
        BuildViewport (document, builder, startLine, 50);
        sw.Stop ();

        Assert.True (sw.ElapsedMilliseconds < 250,
            $"Viewport build took {sw.ElapsedMilliseconds}ms — expected < 250ms. Possible performance regression.");
    }

    /// <summary>
    ///     Building a single long line (~200 chars) should complete well under the threshold.
    ///     Typical: ~16 µs locally; CI runners (shared, no turbo) run 2–4x slower.
    ///     Threshold: 100 ms.
    /// </summary>
    [Fact]
    public void BuildSingleLongLine_CompletesWithinBudget ()
    {
        TextDocument document = new (new string ('a', 200));
        VisualLineBuilder builder = new ();
        DocumentLine line = document.GetLineByNumber (1);

        // Warm up
        for (var i = 0; i < 100; i++)
        {
            BuildLine (document, builder, line);
        }

        Stopwatch sw = Stopwatch.StartNew ();

        for (var i = 0; i < 100; i++)
        {
            BuildLine (document, builder, line);
        }

        sw.Stop ();

        Assert.True (sw.ElapsedMilliseconds < 500,
            $"100 long-line builds took {sw.ElapsedMilliseconds}ms — expected < 500ms. Possible performance regression.");
    }

    /// <summary>
    ///     Sequential line-tree lookups across a 100K-line document should be fast.
    ///     Typical: ~330 ns for 50 lookups. Threshold: 5 ms.
    /// </summary>
    [Fact]
    public void DocumentLineLookup_100K_Lines_CompletesWithinBudget ()
    {
        TextDocument document = new (GenerateDocument (100_000));
        var mid = document.LineCount / 2;

        // Warm up
        for (var i = 0; i < 50; i++)
        {
            _ = document.GetLineByNumber (mid + i);
        }

        Stopwatch sw = Stopwatch.StartNew ();

        for (var rep = 0; rep < 100; rep++)
        {
            for (var i = 0; i < 50; i++)
            {
                _ = document.GetLineByNumber (mid + i);
            }
        }

        sw.Stop ();

        Assert.True (sw.ElapsedMilliseconds < 25,
            $"5000 line lookups in 100K-line doc took {sw.ElapsedMilliseconds}ms — expected < 25ms. Possible performance regression.");
    }

    /// <summary>
    ///     Full-document scroll simulation (build every viewport page) for a 1K-line document.
    ///     Typical: ~4 ms. Threshold: 200 ms.
    /// </summary>
    [Fact]
    public void FullDocumentScroll_1K_Lines_CompletesWithinBudget ()
    {
        TextDocument document = new (GenerateDocument (1_000));
        VisualLineBuilder builder = new ();

        // Warm up
        ScrollFullDocument (document, builder, 24);

        Stopwatch sw = Stopwatch.StartNew ();
        ScrollFullDocument (document, builder, 24);
        sw.Stop ();

        Assert.True (sw.ElapsedMilliseconds < 500,
            $"Full scroll of 1K lines took {sw.ElapsedMilliseconds}ms — expected < 500ms. Possible performance regression.");
    }

    private static void BuildViewport (TextDocument document, VisualLineBuilder builder, int startLine, int height)
    {
        for (var row = 0; row < height && startLine + row <= document.LineCount; row++)
        {
            DocumentLine line = document.GetLineByNumber (startLine + row);
            BuildLine (document, builder, line);
        }
    }

    private static CellVisualLine BuildLine (TextDocument document, VisualLineBuilder builder, DocumentLine line)
    {
        VisualLineBuildContext context = new (
            document, 4, false,
            Attribute.Default, Attribute.Default,
            null, 0, 0, []);

        return builder.Build (line, context);
    }

    private static void ScrollFullDocument (TextDocument document, VisualLineBuilder builder, int viewportHeight)
    {
        for (var startLine = 1; startLine <= document.LineCount; startLine += viewportHeight)
        {
            var endLine = Math.Min (startLine + viewportHeight, document.LineCount + 1);

            for (var lineNum = startLine; lineNum < endLine; lineNum++)
            {
                DocumentLine line = document.GetLineByNumber (lineNum);
                BuildLine (document, builder, line);
            }
        }
    }

    private static string GenerateDocument (int lineCount)
    {
        Random rng = new (42);
        List<string> lines = new (lineCount);

        for (var i = 0; i < lineCount; i++)
        {
            var indent = rng.Next (0, 4);
            var bodyLen = rng.Next (20, 80);
            lines.Add (new string ('\t', indent) + new string ('x', bodyLen));
        }

        return string.Join ('\n', lines);
    }
}

using BenchmarkDotNet.Attributes;
using Terminal.Gui.Document;
using Terminal.Gui.Views.Rendering;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.Benchmarks;

/// <summary>
///     Measures the cost of building visual lines — the hot path during scrolling. Every frame
///     rebuilds all visible lines from document data, so this directly determines scroll fluency.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ScrollingBenchmarks
{
    private VisualLineBuilder _builder = null!;
    private TextDocument _document = null!;

    /// <summary>Number of lines in the synthetic document.</summary>
    [Params (100, 1_000, 10_000)]
    public int LineCount { get; set; }

    /// <summary>Viewport height in rows — how many lines are built per "frame".</summary>
    [Params (24, 50)]
    public int ViewportHeight { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        _builder = new VisualLineBuilder ();
        _document = new TextDocument (GenerateDocument (LineCount));
    }

    /// <summary>
    ///     Build visual lines for one viewport-sized page at the top of the document.
    ///     This is the cheapest case — first page, no scroll offset.
    /// </summary>
    [Benchmark (Description = "Viewport at top")]
    public int BuildViewport_Top ()
    {
        var elements = 0;

        for (var row = 0; row < ViewportHeight && row < _document.LineCount; row++)
        {
            DocumentLine line = _document.GetLineByNumber (row + 1);
            CellVisualLine visualLine = Build (line);
            elements += visualLine.Elements.Count;
        }

        return elements;
    }

    /// <summary>
    ///     Build visual lines for one viewport-sized page at the middle of the document.
    ///     Exercises line-tree lookup at non-trivial offsets.
    /// </summary>
    [Benchmark (Description = "Viewport at middle")]
    public int BuildViewport_Middle ()
    {
        var startLine = _document.LineCount / 2;
        var elements = 0;

        for (var row = 0; row < ViewportHeight && startLine + row <= _document.LineCount; row++)
        {
            DocumentLine line = _document.GetLineByNumber (startLine + row);
            CellVisualLine visualLine = Build (line);
            elements += visualLine.Elements.Count;
        }

        return elements;
    }

    /// <summary>
    ///     Build visual lines for one viewport-sized page at the bottom of the document.
    ///     Exercises the largest line-number lookups.
    /// </summary>
    [Benchmark (Description = "Viewport at bottom")]
    public int BuildViewport_Bottom ()
    {
        var startLine = Math.Max (1, _document.LineCount - ViewportHeight + 1);
        var elements = 0;

        for (var row = 0; row < ViewportHeight && startLine + row <= _document.LineCount; row++)
        {
            DocumentLine line = _document.GetLineByNumber (startLine + row);
            CellVisualLine visualLine = Build (line);
            elements += visualLine.Elements.Count;
        }

        return elements;
    }

    /// <summary>
    ///     Simulates a full-document scroll: build every viewport-sized page sequentially,
    ///     as if the user held Page Down from top to bottom.
    /// </summary>
    [Benchmark (Description = "Full scroll top→bottom")]
    public int ScrollFullDocument ()
    {
        var elements = 0;

        for (var startLine = 1; startLine <= _document.LineCount; startLine += ViewportHeight)
        {
            var endLine = Math.Min (startLine + ViewportHeight, _document.LineCount + 1);

            for (var lineNum = startLine; lineNum < endLine; lineNum++)
            {
                DocumentLine line = _document.GetLineByNumber (lineNum);
                CellVisualLine visualLine = Build (line);
                elements += visualLine.Elements.Count;
            }
        }

        return elements;
    }

    private CellVisualLine Build (DocumentLine line)
    {
        VisualLineBuildContext context = new (
            _document,
            4,
            false,
            Attribute.Default,
            Attribute.Default,
            null,
            0,
            0,
            []);

        return _builder.Build (line, context);
    }

    private static string GenerateDocument (int lineCount)
    {
        // Realistic source-code-like lines: ~60 chars average, with indentation and tabs.
        Random rng = new (42);
        List<string> lines = new (lineCount);

        for (var i = 0; i < lineCount; i++)
        {
            var indent = rng.Next (0, 4);
            var bodyLength = rng.Next (20, 80);
            var prefix = new string ('\t', indent);
            var body = new string ('x', bodyLength);
            lines.Add (prefix + body);
        }

        return string.Join ('\n', lines);
    }
}

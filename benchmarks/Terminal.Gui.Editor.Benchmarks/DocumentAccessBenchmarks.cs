using BenchmarkDotNet.Attributes;
using Terminal.Gui.Text.Document;

namespace Terminal.Gui.Editor.Benchmarks;

/// <summary>
///     Measures the cost of document-layer operations that feed the rendering pipeline:
///     line-tree lookups and rope text extraction. These run once per visible line per frame.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class DocumentAccessBenchmarks
{
    private TextDocument _document = null!;

    [Params (1_000, 10_000, 100_000)] public int LineCount { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        // Realistic line lengths: 40–80 chars with indentation
        Random rng = new (42);
        List<string> lines = new (LineCount);

        for (var i = 0; i < LineCount; i++)
        {
            lines.Add (new string (' ', rng.Next (0, 16)) + new string ('x', rng.Next (30, 70)));
        }

        _document = new TextDocument (string.Join ('\n', lines));
    }

    /// <summary>Sequential line lookup — the access pattern when drawing a viewport.</summary>
    [Benchmark (Description = "GetLineByNumber × 50 (sequential)")]
    public int SequentialLineLookup ()
    {
        var total = 0;
        var start = _document.LineCount / 2;

        for (var i = 0; i < 50 && start + i <= _document.LineCount; i++)
        {
            DocumentLine line = _document.GetLineByNumber (start + i);
            total += line.Length;
        }

        return total;
    }

    /// <summary>Random line lookup — worst case for the balanced tree.</summary>
    [Benchmark (Description = "GetLineByNumber × 50 (random)")]
    public int RandomLineLookup ()
    {
        Random rng = new (123);
        var total = 0;

        for (var i = 0; i < 50; i++)
        {
            var lineNum = rng.Next (1, _document.LineCount + 1);
            DocumentLine line = _document.GetLineByNumber (lineNum);
            total += line.Length;
        }

        return total;
    }

    /// <summary>GetLineByOffset — used by caret positioning during scroll.</summary>
    [Benchmark (Description = "GetLineByOffset × 50 (random)")]
    public int RandomOffsetLookup ()
    {
        Random rng = new (456);
        var total = 0;

        for (var i = 0; i < 50; i++)
        {
            var offset = rng.Next (0, _document.TextLength);
            DocumentLine line = _document.GetLineByOffset (offset);
            total += line.LineNumber;
        }

        return total;
    }

    /// <summary>Extract line text via rope — runs once per visible line per draw.</summary>
    [Benchmark (Description = "GetText × 50 (sequential)")]
    public int SequentialGetText ()
    {
        var total = 0;
        var start = _document.LineCount / 2;

        for (var i = 0; i < 50 && start + i <= _document.LineCount; i++)
        {
            DocumentLine line = _document.GetLineByNumber (start + i);
            var text = _document.GetText (line);
            total += text.Length;
        }

        return total;
    }
}

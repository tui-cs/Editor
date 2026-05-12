using BenchmarkDotNet.Attributes;
using Terminal.Gui.Editor.Document;
using Terminal.Gui.Views.Rendering;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.Benchmarks;

/// <summary>
///     Measures the cost of building a single visual line under varying content. This is the
///     innermost hot loop — one call per visible line per frame.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class VisualLineBuildBenchmarks
{
    private VisualLineBuilder _builder = null!;
    private TextDocument _emojiDoc = null!;
    private TextDocument _longDoc = null!;
    private TextDocument _mixedDoc = null!;

    private TextDocument _shortDoc = null!;
    private TextDocument _tabDoc = null!;

    [GlobalSetup]
    public void Setup ()
    {
        _builder = new VisualLineBuilder ();

        // ~40 chars, typical short line
        _shortDoc = new TextDocument ("    var result = Compute (x, y);");

        // ~200 chars, long line (minified JS, log output, etc.)
        _longDoc = new TextDocument (new string ('a', 200));

        // Tabs: 4 tabs then content
        _tabDoc = new TextDocument ("\t\t\t\tif (condition) { DoSomething (); }");

        // Emoji / ZWJ sequences: grapheme cluster processing
        _emojiDoc = new TextDocument ("👩‍💻 writes 🧑‍🤝‍🧑 code with 🏳️‍🌈 pride");

        // Mixed: tabs + ASCII + wide chars + emoji
        _mixedDoc = new TextDocument ("\t\tvar 名前 = \"👋 hello\";  // コメント");
    }

    [Benchmark (Description = "Short ASCII (~40 chars)", Baseline = true)]
    public int BuildLine_Short ()
    {
        return Build (_shortDoc).Elements.Count;
    }

    [Benchmark (Description = "Long ASCII (~200 chars)")]
    public int BuildLine_Long ()
    {
        return Build (_longDoc).Elements.Count;
    }

    [Benchmark (Description = "Tabbed line (4 tabs + code)")]
    public int BuildLine_Tabs ()
    {
        return Build (_tabDoc).Elements.Count;
    }

    [Benchmark (Description = "Emoji / ZWJ clusters")]
    public int BuildLine_Emoji ()
    {
        return Build (_emojiDoc).Elements.Count;
    }

    [Benchmark (Description = "Mixed: tabs + CJK + emoji")]
    public int BuildLine_Mixed ()
    {
        return Build (_mixedDoc).Elements.Count;
    }

    private CellVisualLine Build (TextDocument doc)
    {
        DocumentLine line = doc.GetLineByNumber (1);

        VisualLineBuildContext context = new (
            doc,
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
}

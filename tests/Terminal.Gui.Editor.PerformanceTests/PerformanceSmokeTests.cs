using System.Diagnostics;
using System.Drawing;
using Ted;
using Terminal.Gui.Document;
using Terminal.Gui.Editor.Rendering;
using Terminal.Gui.Input;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.PerformanceTests;

/// <summary>
///     Stopwatch-based performance smoke tests. Lives in its own csproj and is driven by the
///     dedicated <c>.github/workflows/perf.yml</c> workflow on ubuntu-latest only — Windows /
///     macOS GitHub-hosted runners are too noisy for meaningful timing assertions. Thresholds
///     are deliberately loose (~5× typical wall time on a fast machine) so they only fail on
///     catastrophic regressions, not CI jitter. For precision measurements use the
///     BenchmarkDotNet suite in <c>benchmarks/</c>.
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
    ///     Typing through a vertical caret block far down a large file must not rebuild and scan
    ///     the full visible-line map once per caret. Typical: tens of milliseconds. Threshold: 750 ms.
    /// </summary>
    [Fact]
    public void VerticalMultiCaret_Insert_FarDownLargeDocument_CompletesWithinBudget ()
    {
        TextDocument document = new (GenerateDocument (100_000));
        Editor editor = new () { Document = document, Width = 120, Height = 40 };
        editor.Viewport = new Rectangle (0, 49_980, 120, 40);
        DocumentLine startLine = document.GetLineByNumber (50_000);
        editor.CaretOffset = startLine.Offset + 8;

        for (var i = 0; i < 99; i++)
        {
            editor.InvokeCommand (Command.InsertCaretBelow);
        }

        Assert.Equal (99, editor.AdditionalCaretOffsets.Count);

        // Warm up the key path and undo the edit so the timed operation starts from the same shape.
        editor.NewKeyDownEvent (new Key ('Z'));
        document.UndoStack.Undo ();

        Stopwatch sw = Stopwatch.StartNew ();
        editor.NewKeyDownEvent (new Key ('Z'));
        sw.Stop ();

        Assert.Equal ('Z', document.GetCharAt (startLine.Offset + 8));
        Assert.True (sw.ElapsedMilliseconds < 750,
            $"Typing at 100 vertical carets near line 50K took {sw.ElapsedMilliseconds}ms — expected < 750ms.");
    }

    /// <summary>
    ///     Ted wires brace folding and syntax metadata around the editor. A multi-caret type must not
    ///     rescan foldings once per caret; folding refreshes at the document update boundary.
    /// </summary>
    [Fact]
    public void TedAppCs_VerticalMultiCaret_TabThenType_CompletesWithinBudget ()
    {
        Editor editor = CreateTedAppCsVerticalCaretReproEditor ();
        editor.InvokeCommand (Command.InsertTab);

        Stopwatch sw = Stopwatch.StartNew ();

        foreach (var ch in "test")
        {
            editor.NewKeyDownEvent (new Key (ch));
        }

        sw.Stop ();

        Assert.Contains ("test", editor.Document!.Text);
        Assert.True (sw.ElapsedMilliseconds < 750,
            $"Typing \"test\" in TedApp.cs at 6 vertical carets took {sw.ElapsedMilliseconds}ms — expected < 750ms.");
    }

    /// <summary>
    ///     Backspace and undo after vertical multi-caret typing in Ted must not replay the old
    ///     one-document-change-per-caret/folding-offset-update hot path.
    /// </summary>
    [Fact]
    public void TedAppCs_VerticalMultiCaret_BackspaceAndUndo_CompletesWithinBudget ()
    {
        Editor editor = CreateTedAppCsVerticalCaretReproEditor ();
        editor.InvokeCommand (Command.InsertTab);

        foreach (var ch in "test")
        {
            editor.NewKeyDownEvent (new Key (ch));
        }

        Stopwatch backspace = Stopwatch.StartNew ();
        editor.InvokeCommand (Command.DeleteCharLeft);
        backspace.Stop ();

        Assert.True (backspace.ElapsedMilliseconds < 750,
            $"Backspace in TedApp.cs at 6 vertical carets took {backspace.ElapsedMilliseconds}ms — expected < 750ms.");

        Stopwatch undoBackspace = Stopwatch.StartNew ();
        editor.InvokeCommand (Command.Undo);
        undoBackspace.Stop ();

        Assert.True (undoBackspace.ElapsedMilliseconds < 750,
            $"Undoing Backspace in TedApp.cs at 6 vertical carets took {undoBackspace.ElapsedMilliseconds}ms — expected < 750ms.");

        editor.NewKeyDownEvent (new Key ('x'));

        Stopwatch undoTyping = Stopwatch.StartNew ();
        editor.InvokeCommand (Command.Undo);
        undoTyping.Stop ();

        Assert.True (undoTyping.ElapsedMilliseconds < 750,
            $"Undoing typing in TedApp.cs at 6 vertical carets took {undoTyping.ElapsedMilliseconds}ms — expected < 750ms.");
    }

    private static Editor CreateTedAppCsVerticalCaretReproEditor ()
    {
        var filePath = FindRepoFile (Path.Combine ("examples", "ted", "TedApp.cs"));
        TedApp app = new (configPath: Path.Combine (Path.GetTempPath (), $"ted-perf-{Guid.NewGuid ():N}.json"));
        app.ShowOpenDialog = () => filePath;

        Assert.True (app.OpenFile ());

        Editor editor = app.Editor;
        DocumentLine line = editor.Document!.GetLineByNumber (51);
        editor.CaretOffset = line.Offset + 12;

        for (var i = 0; i < 5; i++)
        {
            editor.InvokeCommand (Command.InsertCaretBelow);
        }

        Assert.Equal (5, editor.AdditionalCaretOffsets.Count);

        return editor;
    }

    private static string FindRepoFile (string relativePath)
    {
        DirectoryInfo? dir = new (Directory.GetCurrentDirectory ());

        while (dir is not null)
        {
            var candidate = Path.Combine (dir.FullName, relativePath);

            if (File.Exists (candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException ($"Could not find {relativePath} from {Directory.GetCurrentDirectory ()}.");
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

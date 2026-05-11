using BenchmarkDotNet.Attributes;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;

namespace Terminal.Gui.Editor.Benchmarks;

/// <summary>
///     Measures the cost of caret movement through the full pipeline: key injection →
///     command dispatch → caret offset update → EnsureCaretVisible → viewport scroll →
///     layout/draw. Focuses on the transition points where the caret reaches a viewport
///     boundary and forces a scroll.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CaretMovementBenchmarks
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    private string _documentText = null!;

    [Params (1_000)]
    public int LineCount { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        _documentText = GenerateDocument (LineCount);
    }

    /// <summary>
    ///     Move caret down one line at a time, counting how many moves trigger a viewport
    ///     scroll (i.e. the caret hits the bottom edge). Returns the number of scroll events.
    /// </summary>
    [Benchmark (Description = "Caret ↓ with scroll counting")]
    public int CaretDown_CountScrolls ()
    {
        using EditorHarness h = new (_documentText);
        var lastLineIndex = h.Editor.Document!.LineCount - 1;
        var scrolls = 0;

        while (true)
        {
            var viewportYBefore = h.Editor.Viewport.Y;

            h.Injector.InjectKey (Key.CursorDown, Direct);
            h.Render ();

            if (h.Editor.Viewport.Y != viewportYBefore)
            {
                scrolls++;
            }

            if (GetCaretLineIndex (h) >= lastLineIndex)
            {
                break;
            }
        }

        return scrolls;
    }

    /// <summary>
    ///     Move caret up from bottom, counting viewport scrolls triggered when the caret
    ///     hits the top edge.
    /// </summary>
    [Benchmark (Description = "Caret ↑ with scroll counting")]
    public int CaretUp_CountScrolls ()
    {
        using EditorHarness h = new (_documentText);
        h.Editor.CaretOffset = h.Editor.Document!.TextLength;
        h.Render ();
        var scrolls = 0;

        while (true)
        {
            var viewportYBefore = h.Editor.Viewport.Y;

            h.Injector.InjectKey (Key.CursorUp, Direct);
            h.Render ();

            if (h.Editor.Viewport.Y != viewportYBefore)
            {
                scrolls++;
            }

            if (GetCaretLineIndex (h) <= 0)
            {
                break;
            }
        }

        return scrolls;
    }

    /// <summary>
    ///     Move caret right through a long line, counting viewport horizontal scrolls
    ///     triggered when the caret hits the right edge.
    /// </summary>
    [Benchmark (Description = "Caret → horizontal scroll")]
    public int CaretRight_HorizontalScroll ()
    {
        // Single very long line in a narrow viewport to force many horizontal scrolls.
        using EditorHarness h = new (new string ('A', 500), width: 40, height: 5);
        var docLength = h.Editor.Document!.TextLength;
        var scrolls = 0;

        while (h.Editor.CaretOffset < docLength)
        {
            var viewportXBefore = h.Editor.Viewport.X;

            h.Injector.InjectKey (Key.CursorRight, Direct);
            h.Render ();

            if (h.Editor.Viewport.X != viewportXBefore)
            {
                scrolls++;
            }
        }

        return scrolls;
    }

    /// <summary>
    ///     Move caret left through a long line from the end, counting horizontal scrolls.
    /// </summary>
    [Benchmark (Description = "Caret ← horizontal scroll")]
    public int CaretLeft_HorizontalScroll ()
    {
        using EditorHarness h = new (new string ('A', 500), width: 40, height: 5);
        h.Editor.CaretOffset = h.Editor.Document!.TextLength;
        h.Render ();
        var scrolls = 0;

        while (h.Editor.CaretOffset > 0)
        {
            var viewportXBefore = h.Editor.Viewport.X;

            h.Injector.InjectKey (Key.CursorLeft, Direct);
            h.Render ();

            if (h.Editor.Viewport.X != viewportXBefore)
            {
                scrolls++;
            }
        }

        return scrolls;
    }

    /// <summary>
    ///     Home/End alternation — caret jumps between column 0 and end-of-line on a long
    ///     line, triggering maximum horizontal viewport shifts per keystroke.
    /// </summary>
    [Benchmark (Description = "Home/End oscillation (500 reps)")]
    public int HomeEndOscillation ()
    {
        using EditorHarness h = new (new string ('B', 200), width: 40, height: 5);
        var totalScrolls = 0;

        for (var i = 0; i < 500; i++)
        {
            var xBefore = h.Editor.Viewport.X;
            h.Injector.InjectKey (i % 2 == 0 ? Key.End : Key.Home, Direct);
            h.Render ();

            if (h.Editor.Viewport.X != xBefore)
            {
                totalScrolls++;
            }
        }

        return totalScrolls;
    }

    /// <summary>
    ///     Ctrl+Home / Ctrl+End alternation — caret jumps between start and end of a large
    ///     document, forcing maximum vertical viewport shifts per keystroke.
    /// </summary>
    [Benchmark (Description = "Ctrl+Home/End oscillation (100 reps)")]
    public int CtrlHomeEndOscillation ()
    {
        using EditorHarness h = new (_documentText);
        var totalScrolls = 0;

        for (var i = 0; i < 100; i++)
        {
            var yBefore = h.Editor.Viewport.Y;
            h.Injector.InjectKey (i % 2 == 0 ? Key.End.WithCtrl : Key.Home.WithCtrl, Direct);
            h.Render ();

            if (h.Editor.Viewport.Y != yBefore)
            {
                totalScrolls++;
            }
        }

        return totalScrolls;
    }

    private static int GetCaretLineIndex (EditorHarness h)
    {
        return h.Editor.Document!.GetLineByOffset (h.Editor.CaretOffset).LineNumber - 1;
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

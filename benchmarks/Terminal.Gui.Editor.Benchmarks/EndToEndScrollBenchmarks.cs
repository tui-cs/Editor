using BenchmarkDotNet.Attributes;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;

namespace Terminal.Gui.Editor.Benchmarks;

/// <summary>
///     End-to-end scrolling benchmarks that exercise the full input → caret move → viewport
///     scroll → layout → draw pipeline. Each benchmark injects real key events and renders
///     after every keypress, measuring the total wall time to scroll through the document.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class EndToEndScrollBenchmarks
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    private string _documentText = null!;

    [Params (500, 5_000)]
    public int LineCount { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        _documentText = GenerateDocument (LineCount, lineLength: 80);
    }

    /// <summary>
    ///     Arrow-down from top until the viewport reaches the bottom of the document.
    ///     Each iteration: inject CursorDown → render → check viewport.
    /// </summary>
    [Benchmark (Description = "Arrow ↓ to bottom")]
    public int ScrollDown_ToBottom ()
    {
        using EditorHarness h = new (_documentText);
        var lastLineIndex = h.Editor.Document!.LineCount - 1;
        var renders = 0;

        while (true)
        {
            h.Injector.InjectKey (Key.CursorDown, Direct);
            h.Render ();
            renders++;

            var caretLine = GetCaretLineIndex (h);

            if (caretLine >= lastLineIndex)
            {
                break;
            }
        }

        return renders;
    }

    /// <summary>
    ///     Arrow-up from bottom to top. Positions the caret at the last line first,
    ///     then scrolls upward until the viewport reaches the top.
    /// </summary>
    [Benchmark (Description = "Arrow ↑ to top")]
    public int ScrollUp_ToTop ()
    {
        using EditorHarness h = new (_documentText);
        h.Editor.CaretOffset = h.Editor.Document!.TextLength;
        h.Render ();
        var renders = 0;

        while (true)
        {
            h.Injector.InjectKey (Key.CursorUp, Direct);
            h.Render ();
            renders++;

            var caretLine = GetCaretLineIndex (h);

            if (caretLine <= 0)
            {
                break;
            }
        }

        return renders;
    }

    /// <summary>
    ///     Arrow-right from the start of the document until the caret reaches the end.
    ///     Exercises horizontal scrolling and line-wrap transitions.
    /// </summary>
    [Benchmark (Description = "Arrow → to end (100 lines)")]
    public int ScrollRight_ToEnd ()
    {
        // Use a smaller document for right-scroll — traversing char-by-char through
        // 5K lines would be millions of iterations.
        using EditorHarness h = new (GenerateDocument (100, lineLength: 120), width: 40);
        var docLength = h.Editor.Document!.TextLength;
        var renders = 0;

        while (h.Editor.CaretOffset < docLength)
        {
            h.Injector.InjectKey (Key.CursorRight, Direct);
            h.Render ();
            renders++;
        }

        return renders;
    }

    /// <summary>
    ///     Arrow-left from the end of the document back to position 0.
    ///     Exercises reverse horizontal scrolling and line transitions.
    /// </summary>
    [Benchmark (Description = "Arrow ← to start (100 lines)")]
    public int ScrollLeft_ToStart ()
    {
        using EditorHarness h = new (GenerateDocument (100, lineLength: 120), width: 40);
        h.Editor.CaretOffset = h.Editor.Document!.TextLength;
        h.Render ();
        var renders = 0;

        while (h.Editor.CaretOffset > 0)
        {
            h.Injector.InjectKey (Key.CursorLeft, Direct);
            h.Render ();
            renders++;
        }

        return renders;
    }

    /// <summary>
    ///     PageDown from top to bottom — fewer iterations than arrow-down, but each
    ///     iteration redraws a full page of new content.
    /// </summary>
    [Benchmark (Description = "PageDown to bottom")]
    public int PageDown_ToBottom ()
    {
        using EditorHarness h = new (_documentText);
        var lastLineIndex = h.Editor.Document!.LineCount - 1;
        var renders = 0;

        while (true)
        {
            h.Injector.InjectKey (Key.PageDown, Direct);
            h.Render ();
            renders++;

            var caretLine = GetCaretLineIndex (h);

            if (caretLine >= lastLineIndex)
            {
                break;
            }
        }

        return renders;
    }

    /// <summary>
    ///     PageUp from bottom to top.
    /// </summary>
    [Benchmark (Description = "PageUp to top")]
    public int PageUp_ToTop ()
    {
        using EditorHarness h = new (_documentText);
        h.Editor.CaretOffset = h.Editor.Document!.TextLength;
        h.Render ();
        var renders = 0;

        while (true)
        {
            h.Injector.InjectKey (Key.PageUp, Direct);
            h.Render ();
            renders++;

            var caretLine = GetCaretLineIndex (h);

            if (caretLine <= 0)
            {
                break;
            }
        }

        return renders;
    }

    private static int GetCaretLineIndex (EditorHarness h)
    {
        return h.Editor.Document!.GetLineByOffset (h.Editor.CaretOffset).LineNumber - 1;
    }

    private static string GenerateDocument (int lineCount, int lineLength)
    {
        Random rng = new (42);
        List<string> lines = new (lineCount);

        for (var i = 0; i < lineCount; i++)
        {
            var indent = rng.Next (0, 4);
            var bodyLen = Math.Max (1, lineLength - indent * 4);
            lines.Add (new string ('\t', indent) + new string ('x', bodyLen));
        }

        return string.Join ('\n', lines);
    }
}

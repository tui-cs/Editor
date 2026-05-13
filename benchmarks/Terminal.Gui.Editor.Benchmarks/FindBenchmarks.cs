using BenchmarkDotNet.Attributes;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Search;

namespace Terminal.Gui.Editor.Benchmarks;

/// <summary>
///     Measures Find / Replace engine cost. The new <see cref="ISearchStrategy" />-driven path is
///     compared against the legacy <c>document.Text.IndexOf</c> loop the <see cref="Editor" /> used
///     before PR #76. Both paths operate directly on <see cref="TextDocument" /> — no <c>Editor</c>
///     wrapper, no event handlers, no visual-line caches — so the comparison isolates the engine
///     itself. The Editor's per-edit notification cost is real but separate, and would apply to
///     both engines identically once landed.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class FindBenchmarks
{
    private const string Needle = "FINDME";
    private TextDocument _document = null!;
    private ISearchStrategy _strategy = null!;

    /// <summary>Approximate document size in characters.</summary>
    [Params (100_000)]
    public int DocSize { get; set; }

    /// <summary>Number of matches sprinkled through the document.</summary>
    [Params (10, 100, 1_000)]
    public int MatchCount { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        // Build a document of DocSize characters with MatchCount evenly spaced occurrences of Needle.
        // The remaining bulk is benign filler so the regex / IndexOf scans actually have ground to cover.
        var gap = Math.Max (1, (DocSize - MatchCount * Needle.Length) / Math.Max (1, MatchCount));
        Span<char> filler = stackalloc char[Math.Min (gap, 4096)];
        filler.Fill ('x');
        var fillerStr = new string (filler);

        using StringWriter sw = new ();

        for (var i = 0; i < MatchCount; i++)
        {
            var remaining = gap;

            while (remaining > 0)
            {
                var chunk = Math.Min (remaining, fillerStr.Length);
                sw.Write (fillerStr.AsSpan (0, chunk));
                remaining -= chunk;
            }

            sw.Write (Needle);
        }

        _document = new TextDocument (sw.ToString ());
        _strategy = SearchStrategyFactory.Create (Needle, false, false, SearchMode.Normal);
    }

    /// <summary>
    ///     FindNext via the new <see cref="ISearchStrategy" /> seam — one search call, as the find dialog would invoke
    ///     per keystroke.
    /// </summary>
    [Benchmark (Description = "FindNext — new (ISearchStrategy)")]
    public int FindNext_New ()
    {
        ISearchResult? r = _strategy.FindNext (_document, 0, _document.TextLength);

        return r?.Offset ?? -1;
    }

    /// <summary>FindNext using the legacy materialize-then-IndexOf approach the engine used to take.</summary>
    [Benchmark (Description = "FindNext — old (Text.IndexOf)", Baseline = true)]
    public int FindNext_Old ()
    {
        return _document.Text.IndexOf (Needle, 0, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ReplaceAll via the new engine: <c>SearchStrategy.FindAll</c> materializes the document once,
    ///     replacements run in reverse under a single <c>RunUpdate ()</c> scope. Direct on the document,
    ///     no Editor wrapper.
    /// </summary>
    [Benchmark (Description = "ReplaceAll — new (FindAll + reverse)")]
    public int ReplaceAll_New ()
    {
        TextDocument doc = new (_document.Text);

        List<ISearchResult> matches = _strategy.FindAll (doc, 0, doc.TextLength).ToList ();

        if (matches.Count == 0)
        {
            return 0;
        }

        using IDisposable scope = doc.RunUpdate ();

        for (var i = matches.Count - 1; i >= 0; i--)
        {
            ISearchResult match = matches[i];
            doc.Replace (match.Offset, match.Length, match.ReplaceWith ("X"));
        }

        return matches.Count;
    }

    /// <summary>
    ///     ReplaceAll using the legacy IndexOf loop: N rope materializations (one per <c>document.Text</c>
    ///     access inside the loop), N <c>Replace</c> calls, all wrapped in <c>RunUpdate ()</c>. This is
    ///     what <c>Editor.FindReplace.cs</c> did before PR #76.
    /// </summary>
    [Benchmark (Description = "ReplaceAll — old (N × IndexOf)")]
    public int ReplaceAll_Old ()
    {
        TextDocument doc = new (_document.Text);
        var count = 0;
        var searchOffset = 0;

        using IDisposable scope = doc.RunUpdate ();

        while (searchOffset < doc.TextLength)
        {
            // The wart this PR fixed: doc.Text materializes the whole rope every loop iteration.
            var matchOffset = doc.Text.IndexOf (Needle, searchOffset, StringComparison.Ordinal);

            if (matchOffset < 0)
            {
                break;
            }

            doc.Replace (matchOffset, Needle.Length, "X");
            searchOffset = matchOffset + 1;
            count++;
        }

        return count;
    }
}

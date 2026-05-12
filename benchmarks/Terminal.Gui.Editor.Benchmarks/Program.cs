using System.Diagnostics;
using BenchmarkDotNet.Running;
using Terminal.Gui.Editor.Benchmarks;
using Terminal.Gui.Text.Document;
using Terminal.Gui.Text.Search;

if (args.Length > 0 && args[0] == "--quick-find")
{
    QuickFindBench ();

    return;
}

if (args.Length > 0 && args[0] == "--quick-find-incremental")
{
    QuickIncrementalFindBench ();

    return;
}

BenchmarkSwitcher.FromAssembly (typeof (Program).Assembly).Run (args);

static void QuickFindBench ()
{
    // Quick Stopwatch comparison of new ISearchStrategy vs legacy IndexOf-loop for ReplaceAll.
    // BDN gives statistically rigorous numbers but is overkill for "is the new path faster or slower?";
    // this completes in ~10s and prints a comparison the human can read at a glance.
    const string needle = "FINDME";
    const int docSize = 100_000;
    int[] matchCounts = [10, 100, 1_000];
    const int iterations = 20;

    Console.WriteLine ($"Quick Find/Replace bench — DocSize={docSize}, iters={iterations}, replacement=\"X\"");
    Console.WriteLine ();
    Console.WriteLine ($"{"MatchCount",10}  {"Path",-32}  {"Mean (ms)",10}  {"Min (ms)",10}  {"Allocated (KB/op)",18}");
    Console.WriteLine (new string ('-', 88));

    foreach (var matchCount in matchCounts)
    {
        var doc = BuildDoc (needle, docSize, matchCount);
        var strategy = SearchStrategyFactory.Create (needle, ignoreCase: false, matchWholeWords: false, SearchMode.Normal);

        // Warm both paths.
        ReplaceAllNew (doc, strategy);
        ReplaceAllOld (doc, needle);

        var (newMean, newMin, newAlloc) = TimeIt (iterations, () => ReplaceAllNew (doc, strategy));
        var (oldMean, oldMin, oldAlloc) = TimeIt (iterations, () => ReplaceAllOld (doc, needle));

        Console.WriteLine ($"{matchCount,10}  {"new (FindAll + reverse)",-32}  {newMean,10:F3}  {newMin,10:F3}  {newAlloc / 1024.0,18:F1}");
        Console.WriteLine ($"{matchCount,10}  {"old (N × IndexOf)",-32}  {oldMean,10:F3}  {oldMin,10:F3}  {oldAlloc / 1024.0,18:F1}");
        Console.WriteLine ($"{"",10}  {"  ratio (new/old)",-32}  {newMean / oldMean,10:F2}x  {"",10}  {(double)newAlloc / oldAlloc,18:F2}x");
        Console.WriteLine ();
    }
}

static void QuickIncrementalFindBench ()
{
    // Demonstrates the gui-cs/Editor#82 fix: FindNext advancing through a document one match at
    // a time should not re-scan the prefix [0, offset) on every call. Compares the new path
    // (RegexSearchStrategy.FindAll using Regex.Match(text, startat)) against an inlined
    // simulation of the old upstream behavior (Regex.Matches(text) from index 0, post-filtered
    // by offset). Both paths materialize document.Text once per call — that's the rope cost,
    // which this fix does not address.
    const string needle = "FINDME";
    const int docSize = 1_000_000;
    int[] matchCounts = [50, 200, 1_000];

    Console.WriteLine ($"Quick incremental-find bench — DocSize={docSize}");
    Console.WriteLine ();
    Console.WriteLine ($"{"MatchCount",10}  {"Path",-44}  {"Total (ms)",12}  {"Per call (µs)",14}");
    Console.WriteLine (new string ('-', 90));

    foreach (var matchCount in matchCounts)
    {
        var doc = BuildDoc (needle, docSize, matchCount);
        var strategy = SearchStrategyFactory.Create (needle, ignoreCase: false, matchWholeWords: false, SearchMode.Normal);
        var pattern = new System.Text.RegularExpressions.Regex (
                                                                System.Text.RegularExpressions.Regex.Escape (needle),
                                                                System.Text.RegularExpressions.RegexOptions.Multiline);

        // Warm.
        WalkNew (doc, strategy);
        WalkOldSim (doc, pattern, needle.Length);

        var (newTotal, newCalls) = TimeWalk (() => WalkNew (doc, strategy));
        var (oldTotal, oldCalls) = TimeWalk (() => WalkOldSim (doc, pattern, needle.Length));

        Console.WriteLine ($"{matchCount,10}  {"new (Regex.Match from offset)",-44}  {newTotal,12:F2}  {newTotal * 1000.0 / Math.Max (1, newCalls),14:F2}");
        Console.WriteLine ($"{matchCount,10}  {"old sim (Regex.Matches from 0 + post-filter)",-44}  {oldTotal,12:F2}  {oldTotal * 1000.0 / Math.Max (1, oldCalls),14:F2}");
        Console.WriteLine ($"{"",10}  {"  ratio new/old",-44}  {newTotal / oldTotal,12:F3}x");
        Console.WriteLine ();
    }
}

/// <summary>Walks the document by repeatedly calling the new ISearchStrategy.FindNext (#82 fix).</summary>
static int WalkNew (TextDocument doc, ISearchStrategy strategy)
{
    var calls = 0;
    var offset = 0;

    while (true)
    {
        ISearchResult? r = strategy.FindNext (doc, offset, doc.TextLength - offset);
        calls++;

        if (r is null)
        {
            return calls;
        }

        offset = r.Offset + Math.Max (1, r.Length);
    }
}

/// <summary>
///     Walks the document via an inlined simulation of the upstream behavior — Regex.Matches(text)
///     starting at index 0 every call, post-filtered by offset. This is what
///     <c>RegexSearchStrategy.FindAll</c> did before #82.
/// </summary>
static int WalkOldSim (TextDocument doc, System.Text.RegularExpressions.Regex pattern, int needleLen)
{
    var calls = 0;
    var offset = 0;

    while (true)
    {
        var text = doc.Text;
        var matchOffset = -1;

        foreach (System.Text.RegularExpressions.Match m in pattern.Matches (text))
        {
            if (m.Index >= offset)
            {
                matchOffset = m.Index;

                break;
            }
        }

        calls++;

        if (matchOffset < 0)
        {
            return calls;
        }

        offset = matchOffset + Math.Max (1, needleLen);
    }
}

static (double totalMs, int calls) TimeWalk (Func<int> walk)
{
    GC.Collect ();
    GC.WaitForPendingFinalizers ();
    GC.Collect ();
    var sw = Stopwatch.StartNew ();
    var calls = walk ();
    sw.Stop ();

    return (sw.Elapsed.TotalMilliseconds, calls);
}

static TextDocument BuildDoc (string needle, int docSize, int matchCount)
{
    var gap = Math.Max (1, (docSize - matchCount * needle.Length) / Math.Max (1, matchCount));
    var filler = new string ('x', Math.Min (gap, 4096));
    using var sw = new StringWriter ();

    for (var i = 0; i < matchCount; i++)
    {
        var remaining = gap;

        while (remaining > 0)
        {
            var chunk = Math.Min (remaining, filler.Length);
            sw.Write (filler.AsSpan (0, chunk));
            remaining -= chunk;
        }

        sw.Write (needle);
    }

    return new TextDocument (sw.ToString ());
}

static int ReplaceAllNew (TextDocument source, ISearchStrategy strategy)
{
    var doc = new TextDocument (source.Text);
    var matches = strategy.FindAll (doc, 0, doc.TextLength).ToList ();

    if (matches.Count == 0)
    {
        return 0;
    }

    using var scope = doc.RunUpdate ();

    for (var i = matches.Count - 1; i >= 0; i--)
    {
        var m = matches[i];
        doc.Replace (m.Offset, m.Length, m.ReplaceWith ("X"));
    }

    return matches.Count;
}

static int ReplaceAllOld (TextDocument source, string needle)
{
    var doc = new TextDocument (source.Text);
    var count = 0;
    var searchOffset = 0;
    using var scope = doc.RunUpdate ();

    while (searchOffset < doc.TextLength)
    {
        var matchOffset = doc.Text.IndexOf (needle, searchOffset, StringComparison.Ordinal);

        if (matchOffset < 0)
        {
            break;
        }

        doc.Replace (matchOffset, needle.Length, "X");
        searchOffset = matchOffset + 1;
        count++;
    }

    return count;
}

static (double meanMs, double minMs, long allocBytes) TimeIt (int iterations, Action body)
{
    var times = new double[iterations];

    // Settle: force GC so allocation measurement isolates the workload.
    GC.Collect ();
    GC.WaitForPendingFinalizers ();
    GC.Collect ();

    var alloc0 = GC.GetAllocatedBytesForCurrentThread ();

    for (var i = 0; i < iterations; i++)
    {
        var sw = Stopwatch.StartNew ();
        body ();
        sw.Stop ();
        times[i] = sw.Elapsed.TotalMilliseconds;
    }

    var alloc1 = GC.GetAllocatedBytesForCurrentThread ();
    var allocPerOp = (alloc1 - alloc0) / iterations;

    var sum = 0.0;
    var min = double.MaxValue;

    foreach (var t in times)
    {
        sum += t;

        if (t < min)
        {
            min = t;
        }
    }

    return (sum / iterations, min, allocPerOp);
}

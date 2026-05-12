// Claude - claude-opus-4-7

using System.Linq;
using Terminal.Gui.Text.Document;
using Terminal.Gui.Text.Search;
using Xunit;

namespace Terminal.Gui.Text.Tests.Search;

public class SearchStrategyTests
{
    [Fact]
    public void CaseSensitive_FindsOnlyExactCase ()
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create ("Hello", ignoreCase: false, matchWholeWords: false, SearchMode.Normal);
        TextDocument document = new ("Hello hello HELLO");

        ISearchResult[] results = strategy.FindAll (document, 0, document.TextLength).ToArray ();

        Assert.Single (results);
        Assert.Equal (0, results[0].Offset);
        Assert.Equal (5, results[0].Length);
    }

    [Fact]
    public void CaseInsensitive_FindsAllVariants ()
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create ("hello", ignoreCase: true, matchWholeWords: false, SearchMode.Normal);
        TextDocument document = new ("Hello hello HELLO");

        ISearchResult[] results = strategy.FindAll (document, 0, document.TextLength).ToArray ();

        Assert.Equal (3, results.Length);
        Assert.Equal (0, results[0].Offset);
        Assert.Equal (6, results[1].Offset);
        Assert.Equal (12, results[2].Offset);
    }

    [Fact]
    public void WholeWord_ExcludesSubstringMatches ()
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create ("cat", ignoreCase: false, matchWholeWords: true, SearchMode.Normal);
        TextDocument document = new ("cat catalog scatter");

        ISearchResult[] results = strategy.FindAll (document, 0, document.TextLength).ToArray ();

        Assert.Single (results);
        Assert.Equal (0, results[0].Offset);
    }

    [Fact]
    public void Regex_MatchesPattern ()
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create (@"\d+", ignoreCase: false, matchWholeWords: false, SearchMode.RegEx);
        TextDocument document = new ("abc 123 def 456");

        ISearchResult[] results = strategy.FindAll (document, 0, document.TextLength).ToArray ();

        Assert.Equal (2, results.Length);
        Assert.Equal (4, results[0].Offset);
        Assert.Equal (3, results[0].Length);
        Assert.Equal (12, results[1].Offset);
        Assert.Equal (3, results[1].Length);
    }

    [Fact]
    public void Regex_MatchesAcrossLineBoundary ()
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create (@"end[\r\n]+start", ignoreCase: false, matchWholeWords: false, SearchMode.RegEx);
        TextDocument document = new ("the end\nstart of");

        ISearchResult result = strategy.FindNext (document, 0, document.TextLength);

        Assert.NotNull (result);
        Assert.Equal (4, result.Offset);
        Assert.Equal (9, result.Length);
    }

    [Fact]
    public void Result_OffsetsTrackDocumentEdits_WhenAttachedToCollection ()
    {
        // SearchResult inherits from TextSegment; the consumer (find-and-replace) is expected to
        // attach results to a TextSegmentCollection bound to the document so that offsets shift
        // automatically as the document is edited. Verify the result type cooperates.
        ISearchStrategy strategy = SearchStrategyFactory.Create ("world", ignoreCase: false, matchWholeWords: false, SearchMode.Normal);
        TextDocument document = new ("hello world");

        ISearchResult result = strategy.FindNext (document, 0, document.TextLength);
        Assert.NotNull (result);
        Assert.Equal (6, result.Offset);

        TextSegment segment = Assert.IsAssignableFrom<TextSegment> (result);
        TextSegmentCollection<TextSegment> tracker = new (document);
        tracker.Add (segment);

        document.Insert (0, "say ");

        Assert.Equal (10, segment.StartOffset);
        Assert.Equal (5, segment.Length);
    }

    [Fact]
    public void Wildcard_TranslatesGlobToRegex ()
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create ("f*o", ignoreCase: false, matchWholeWords: false, SearchMode.Wildcard);
        TextDocument document = new ("foo bar fizzo baz");

        ISearchResult[] results = strategy.FindAll (document, 0, document.TextLength).ToArray ();

        Assert.Single (results);
        Assert.Equal (0, results[0].Offset);
        Assert.Equal (13, results[0].Length);
    }

    [Fact]
    public void ReplaceWith_SupportsBackreferences ()
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create (@"(\w+)=(\d+)", ignoreCase: false, matchWholeWords: false, SearchMode.RegEx);
        TextDocument document = new ("count=42");

        ISearchResult result = strategy.FindNext (document, 0, document.TextLength);

        Assert.NotNull (result);
        Assert.Equal ("42:count", result.ReplaceWith ("$2:$1"));
    }

    [Fact]
    public void Equals_ReturnsTrueForSamePatternAndOptions ()
    {
        ISearchStrategy a = SearchStrategyFactory.Create ("foo", ignoreCase: false, matchWholeWords: false, SearchMode.Normal);
        ISearchStrategy b = SearchStrategyFactory.Create ("foo", ignoreCase: false, matchWholeWords: false, SearchMode.Normal);

        Assert.True (a.Equals (b));
    }

    [Fact]
    public void Equals_ReturnsFalseForDifferentCaseSensitivity ()
    {
        ISearchStrategy a = SearchStrategyFactory.Create ("foo", ignoreCase: false, matchWholeWords: false, SearchMode.Normal);
        ISearchStrategy b = SearchStrategyFactory.Create ("foo", ignoreCase: true, matchWholeWords: false, SearchMode.Normal);

        Assert.False (a.Equals (b));
    }

    [Fact]
    public void Equals_ReturnsFalseForDifferentWholeWord ()
    {
        // Pins the Terminal.Gui correctness deviation from upstream — upstream's Equals omits
        // _matchWholeWords, so this assertion would fail without the fork patch. Re-sync from
        // AvaloniaEdit must re-apply the deviation; this test catches a regression.
        ISearchStrategy a = SearchStrategyFactory.Create ("foo", ignoreCase: false, matchWholeWords: false, SearchMode.Normal);
        ISearchStrategy b = SearchStrategyFactory.Create ("foo", ignoreCase: false, matchWholeWords: true, SearchMode.Normal);

        Assert.False (a.Equals (b));
    }

    [Fact]
    public void InvalidRegex_ThrowsSearchPatternException ()
    {
        Assert.Throws<SearchPatternException> (
                                               () => SearchStrategyFactory.Create ("(", ignoreCase: false, matchWholeWords: false, SearchMode.RegEx));
    }

    [Fact]
    public void Create_ThrowsOnEmptyPattern ()
    {
        // Pins the Terminal.Gui correctness deviation from upstream — upstream accepts the
        // empty pattern and compiles to a regex that matches at every position
        // (TextLength+1 zero-length results), a DoS in FindAll/ReplaceAll. Re-sync must
        // re-apply the guard.
        Assert.Throws<ArgumentException> (
                                          () => SearchStrategyFactory.Create (string.Empty, ignoreCase: false, matchWholeWords: false, SearchMode.Normal));
    }

    [Fact]
    public void Create_AcceptsWhitespacePattern ()
    {
        // Whitespace is a legitimate search pattern — the empty-pattern guard must not
        // accidentally reject " " or "\t".
        ISearchStrategy strategy = SearchStrategyFactory.Create (" ", ignoreCase: false, matchWholeWords: false, SearchMode.Normal);
        TextDocument document = new ("a b c");

        ISearchResult[] results = strategy.FindAll (document, 0, document.TextLength).ToArray ();

        Assert.Equal (2, results.Length);
    }

    [Fact]
    public void FindNext_AtNonZeroOffset_ReturnsFirstMatchAtOrAfterOffset ()
    {
        // Pins gui-cs/Editor#82 — FindAll now drives the regex via Regex.Match(text, startat)
        // instead of Matches(text) over the whole document with post-filtering. The observable
        // surface should be identical to the upstream behavior (same match offsets and order);
        // the benchmark catches the perf win separately.
        ISearchStrategy strategy = SearchStrategyFactory.Create ("foo", ignoreCase: false, matchWholeWords: false, SearchMode.Normal);
        TextDocument document = new ("foo bar foo baz foo");

        ISearchResult result = strategy.FindNext (document, 4, document.TextLength - 4);

        Assert.NotNull (result);
        Assert.Equal (8, result.Offset);
    }

    [Fact]
    public void FindNext_MultilineAnchor_OnlyMatchesAtLineStart ()
    {
        // RegexOptions.Multiline is always on (SearchStrategyFactory sets it). The deviation
        // in #82 starts the regex engine at `offset` — verify that `^` still anchors correctly
        // when `offset` lands immediately after a newline, and does NOT anchor when `offset`
        // lands mid-line.
        ISearchStrategy strategy = SearchStrategyFactory.Create (@"^line", ignoreCase: false, matchWholeWords: false, SearchMode.RegEx);
        TextDocument document = new ("line1\nline2\nline3");

        // Whole document: all three "line" prefixes match.
        ISearchResult[] all = strategy.FindAll (document, 0, document.TextLength).ToArray ();
        Assert.Equal (3, all.Length);
        Assert.Equal (0, all[0].Offset);
        Assert.Equal (6, all[1].Offset);
        Assert.Equal (12, all[2].Offset);

        // Starting at offset 6 (just after the first \n): `^` anchors at 6; matches "line2" + "line3".
        ISearchResult[] fromSix = strategy.FindAll (document, 6, document.TextLength - 6).ToArray ();
        Assert.Equal (2, fromSix.Length);
        Assert.Equal (6, fromSix[0].Offset);
        Assert.Equal (12, fromSix[1].Offset);

        // Starting at offset 7 (mid-line, char 'i' of "line2"): `^` does NOT anchor at 7 because
        // text[6] is not a newline. Engine scans forward to next `^` (after the second \n) → 12.
        ISearchResult[] fromSeven = strategy.FindAll (document, 7, document.TextLength - 7).ToArray ();
        Assert.Single (fromSeven);
        Assert.Equal (12, fromSeven[0].Offset);
    }

    [Fact]
    public void FindAll_ZeroLengthMatchPattern_DoesNotInfiniteLoop ()
    {
        // Sanity: a pattern that produces zero-length matches (e.g. `\b` word-boundary). The
        // empty-pattern guard in SearchStrategyFactory.Create blocks the truly degenerate "" case;
        // this test pins the next-most-degenerate case. .NET's Regex.NextMatch() auto-advances by
        // one position for zero-length matches so we don't spin.
        ISearchStrategy strategy = SearchStrategyFactory.Create (@"\b", ignoreCase: false, matchWholeWords: false, SearchMode.RegEx);
        TextDocument document = new ("ab cd");

        // Word boundaries at offsets 0, 2, 3, 5. Stops eventually instead of looping.
        ISearchResult[] results = strategy.FindAll (document, 0, document.TextLength).Take (10).ToArray ();

        Assert.Equal (4, results.Length);
        Assert.All (results, r => Assert.Equal (0, r.Length));
    }

    [Fact]
    public void Range_RestrictsResultsToWindow ()
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create ("x", ignoreCase: false, matchWholeWords: false, SearchMode.Normal);
        TextDocument document = new ("x__x__x");

        ISearchResult[] results = strategy.FindAll (document, 1, 5).ToArray ();

        Assert.Single (results);
        Assert.Equal (3, results[0].Offset);
    }
}

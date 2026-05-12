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
    public void Range_RestrictsResultsToWindow ()
    {
        ISearchStrategy strategy = SearchStrategyFactory.Create ("x", ignoreCase: false, matchWholeWords: false, SearchMode.Normal);
        TextDocument document = new ("x__x__x");

        ISearchResult[] results = strategy.FindAll (document, 1, 5).ToArray ();

        Assert.Single (results);
        Assert.Equal (3, results[0].Offset);
    }
}

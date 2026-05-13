using Terminal.Gui.Editor.Rendering;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Rendering;

public class WordWrapStrategyTests
{
    [Fact]
    public void Short_Line_Returns_Single_Segment ()
    {
        IReadOnlyList<WrapSegment> segments = WordWrapStrategy.ComputeSegments ("hello world", 40, 4);

        Assert.Single (segments);
        Assert.Equal (0, segments[0].StartOffset);
        Assert.Equal (11, segments[0].Length);
    }

    [Fact]
    public void Break_At_Whitespace_Boundary ()
    {
        // "hello world" is 11 chars; with wrap at 8, break after "hello " (6 chars including space).
        IReadOnlyList<WrapSegment> segments = WordWrapStrategy.ComputeSegments ("hello world", 8, 4);

        Assert.Equal (2, segments.Count);
        Assert.Equal (0, segments[0].StartOffset);
        Assert.Equal (6, segments[0].Length); // "hello " including the space
        Assert.Equal (6, segments[1].StartOffset); // "world"
        Assert.Equal (5, segments[1].Length);
    }

    [Fact]
    public void Break_At_Last_Whitespace_Before_Wrap_Column ()
    {
        // "one two three four" with wrap at 10.
        // "one two th" = 10 chars, break at last whitespace after "two " (pos 8).
        IReadOnlyList<WrapSegment> segments = WordWrapStrategy.ComputeSegments ("one two three four", 10, 4);

        Assert.True (segments.Count >= 2);
        Assert.Equal (0, segments[0].StartOffset);
        Assert.Equal (8, segments[0].Length); // "one two " (up to the break point including space)
    }

    [Fact]
    public void Hard_Break_When_No_Whitespace ()
    {
        // 20 characters "a" repeated with no whitespace, wrap at 10.
        var text = new string ('a', 20);
        IReadOnlyList<WrapSegment> segments = WordWrapStrategy.ComputeSegments (text, 10, 4);

        Assert.Equal (2, segments.Count);
        Assert.Equal (0, segments[0].StartOffset);
        Assert.Equal (10, segments[0].Length);
        Assert.Equal (10, segments[1].StartOffset);
        Assert.Equal (10, segments[1].Length);
    }

    [Fact]
    public void Hard_Break_CJK_Double_Width_Characters ()
    {
        // CJK characters are 2 columns wide. 10 CJK chars = 20 columns.
        // With wrap at 12, we fit 6 characters (12 columns) then hard-break.
        var cjk = new string ('\u4e00', 10); // 一一一一一一一一一一
        IReadOnlyList<WrapSegment> segments = WordWrapStrategy.ComputeSegments (cjk, 12, 4);

        Assert.True (segments.Count >= 2);
        Assert.Equal (0, segments[0].StartOffset);
        Assert.Equal (6, segments[0].Length); // 6 CJK chars = 12 columns
    }

    [Fact]
    public void Continuation_Lines_Have_Zero_Leading_Indent ()
    {
        IReadOnlyList<WrapSegment> segments = WordWrapStrategy.ComputeSegments ("hello world foo bar", 8, 4);

        foreach (WrapSegment seg in segments)
        {
            Assert.Equal (0, seg.LeadingIndent);
        }
    }

    [Fact]
    public void Empty_Text_Returns_Single_Empty_Segment ()
    {
        IReadOnlyList<WrapSegment> segments = WordWrapStrategy.ComputeSegments ("", 40, 4);

        Assert.Single (segments);
        Assert.Equal (0, segments[0].StartOffset);
        Assert.Equal (0, segments[0].Length);
    }

    [Fact]
    public void Multiple_Spaces_At_Break_Point_Are_Skipped ()
    {
        // "hello   world" — break at col 8. "hello   " is 8 chars. The space at pos 5 sets
        // lastWhitespaceOffset to 6. When we hit col 8, we break at offset 6, then skip
        // whitespace from pos 6 which skips the remaining spaces to pos 8.
        IReadOnlyList<WrapSegment> segments = WordWrapStrategy.ComputeSegments ("hello   world", 8, 4);

        Assert.Equal (2, segments.Count);
        Assert.Equal (8, segments[1].StartOffset); // "world" starts after all spaces
        Assert.Equal (5, segments[1].Length);
    }
}

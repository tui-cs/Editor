// Claude - claude-opus-4-7

using System.Reflection;
using Terminal.Gui.Document;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views.Rendering;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Vets the default-args <see cref="CellVisualLine" /> cache on <see cref="Views.Editor" />.
///     The cache is a private detail — these tests reach into it via reflection because the
///     invariants are about identity (cache hit returns same instance, invalidation drops it),
///     which the public surface alone can't observe.
/// </summary>
public class EditorVisualLineCacheTests
{
    [Fact]
    public void Caret_Math_Hits_Cache_For_Repeated_Reads ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("alpha\nbeta\ngamma") };

        // GetCaretColumn() reads the cache. Two reads against the same line, no doc change,
        // must return the same CellVisualLine instance.
        editor.CaretOffset = 0;
        CellVisualLine first = ReadCache (editor, 1)!;

        editor.CaretOffset = 3;
        CellVisualLine second = ReadCache (editor, 1)!;

        Assert.Same (first, second);
    }

    [Fact]
    public void Document_Edit_Drops_Affected_Line_From_Cache ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("alpha\nbeta\ngamma") };
        editor.CaretOffset = 0;
        ForceCachePopulation (editor);

        Assert.NotNull (ReadCache (editor, 2));
        CellVisualLine line2Before = ReadCache (editor, 2)!;

        // Edit on line 2 ("beta" at offset 6). The cache entry for line 2 must be dropped; the
        // line-1 entry is upstream of the edit and may stay.
        editor.Document!.Insert (8, "!");

        CellVisualLine? line2After = ReadCache (editor, 2);
        Assert.False (ReferenceEquals (line2Before, line2After),
            "Line 2's cached visual line should be dropped after an edit inside it.");
    }

    [Fact]
    public void Document_Edit_Drops_Downstream_Line_Entries ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("alpha\nbeta\ngamma") };
        ForceCachePopulation (editor);
        CellVisualLine line3Before = ReadCache (editor, 3)!;

        // Newline insertion on line 1 renumbers everything downstream — line 3 is now line 4.
        // Its cached entry under key 3 is stale and must go.
        editor.Document!.Insert (5, "\n");

        CellVisualLine? line3After = ReadCache (editor, 3);
        Assert.False (ReferenceEquals (line3Before, line3After),
            "Cache entries downstream of a newline-bearing edit must be dropped (line numbers shifted).");
    }

    [Fact]
    public void IndentationSize_Change_Drops_Stale_Cached_Lines ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("\talpha\n\tbeta") };
        ForceCachePopulation (editor);
        CellVisualLine line1Before = ReadCache (editor, 1)!;
        var widthBefore = line1Before.VisualLength;

        editor.IndentationSize = 8;
        CellVisualLine line1After = ReadCache (editor, 1)!;

        // After changing tab width, "\talpha" must measure wider; if the cache had returned the
        // pre-change instance, VisualLength would be unchanged.
        Assert.False (ReferenceEquals (line1Before, line1After));
        Assert.NotEqual (widthBefore, line1After.VisualLength);
    }

    [Fact]
    public void ShowTabs_Change_Drops_Stale_Cached_Lines ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("\ta") };
        ForceCachePopulation (editor);
        CellVisualLine before = ReadCache (editor, 1)!;

        editor.ShowTabs = true;
        CellVisualLine after = ReadCache (editor, 1)!;

        Assert.False (ReferenceEquals (before, after));
    }

    [Fact]
    public void Document_Swap_Replaces_Cached_Lines ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("alpha") };
        ForceCachePopulation (editor);
        CellVisualLine line1Before = ReadCache (editor, 1)!;

        editor.Document = new TextDocument ("totally different content");
        CellVisualLine? line1After = ReadCache (editor, 1);

        Assert.False (ReferenceEquals (line1Before, line1After),
            "Swapping Document must drop cached visual lines tied to the old document.");
        Assert.NotEqual (line1Before.VisualLength, line1After!.VisualLength);
    }

    private static CellVisualLine? ReadCache (Views.Editor editor, int lineNumber)
    {
        // Trigger a cache fill via the public API path (caret column → GetOrBuildDefaultVisualLine).
        DocumentLine line = editor.Document!.GetLineByNumber (lineNumber);
        editor.CaretOffset = line.Offset;

        Dictionary<int, CellVisualLine> cache = GetCache (editor);

        return cache.GetValueOrDefault (lineNumber);
    }

    private static void ForceCachePopulation (Views.Editor editor)
    {
        // Walk the caret across every line so each one's default visual line is built and cached.
        for (var i = 1; i <= editor.Document!.LineCount; i++)
        {
            DocumentLine line = editor.Document.GetLineByNumber (i);
            editor.CaretOffset = line.Offset;
        }
    }

    private static IReadOnlyCollection<int> ReadCacheKeys (Views.Editor editor)
    {
        return GetCache (editor).Keys.ToArray ();
    }

    private static Dictionary<int, CellVisualLine> GetCache (Views.Editor editor)
    {
        FieldInfo field = typeof (Views.Editor)
                              .GetField ("_defaultVisualLineCache", BindingFlags.Instance | BindingFlags.NonPublic)
                          ?? throw new InvalidOperationException ("_defaultVisualLineCache field is missing");

        return (Dictionary<int, CellVisualLine>)field.GetValue (editor)!;
    }

    /// <summary>
    ///     Cherry-picked from PR #54: <see cref="Views.Editor.OnDrawingContent" /> can also cache
    ///     when there's no syntax highlighter, no selection, and no transformers. The cache is
    ///     separate from the default-args cache so the two paths don't thrash each other.
    /// </summary>
    public class DrawCache
    {
        [Fact]
        public void Repeated_Draw_Of_Same_Line_With_Same_Attributes_Reuses_Entry ()
        {
            Views.Editor editor = new () { Document = new TextDocument ("alpha") };
            DocumentLine line = editor.Document!.GetLineByNumber (1);

            CellVisualLine first = InvokeDrawBuild (editor, line);
            CellVisualLine second = InvokeDrawBuild (editor, line);

            Assert.Same (first, second);
        }

        [Fact]
        public void Different_Attributes_Bypass_Cache ()
        {
            Views.Editor editor = new () { Document = new TextDocument ("alpha") };
            DocumentLine line = editor.Document!.GetLineByNumber (1);

            Attribute red = new (Color.Red, Color.Black);
            Attribute blue = new (Color.Blue, Color.Black);

            CellVisualLine first = InvokeDrawBuild (editor, line, red, red);
            CellVisualLine second = InvokeDrawBuild (editor, line, blue, blue);

            Assert.NotSame (first, second);
        }

        [Fact]
        public void Selection_Bypasses_Cache ()
        {
            Views.Editor editor = new () { Document = new TextDocument ("alpha") };
            DocumentLine line = editor.Document!.GetLineByNumber (1);

            CellVisualLine first = InvokeDrawBuild (editor, line);
            CellVisualLine withSelection = InvokeDrawBuild (editor, line, selStart: 0, selEnd: 3);

            // With selection active, the eligibility predicate fails; the call falls through to
            // a fresh build rather than returning the cached no-selection entry.
            Assert.NotSame (first, withSelection);
        }

        [Fact]
        public void Document_Edit_Drops_Draw_Cache_From_Affected_Line ()
        {
            Views.Editor editor = new () { Document = new TextDocument ("alpha\nbeta") };
            DocumentLine line2 = editor.Document!.GetLineByNumber (2);
            CellVisualLine before = InvokeDrawBuild (editor, line2);

            editor.Document!.Insert (line2.Offset, "!");

            DocumentLine line2After = editor.Document.GetLineByNumber (2);
            CellVisualLine after = InvokeDrawBuild (editor, line2After);

            Assert.NotSame (before, after);
        }

        private static CellVisualLine InvokeDrawBuild (
            Views.Editor editor,
            DocumentLine line,
            Attribute? normal = null,
            Attribute? selected = null,
            int selStart = 0,
            int selEnd = 0)
        {
            MethodInfo method = typeof (Views.Editor)
                                    .GetMethod (
                                        "GetOrBuildDrawVisualLine",
                                        BindingFlags.Instance | BindingFlags.NonPublic)
                                ?? throw new InvalidOperationException ("GetOrBuildDrawVisualLine missing");

            Attribute n = normal ?? Attribute.Default;
            Attribute s = selected ?? Attribute.Default;

            return (CellVisualLine)method.Invoke (
                editor,
                [line, null!, n, s, selStart, selEnd])!;
        }
    }
}

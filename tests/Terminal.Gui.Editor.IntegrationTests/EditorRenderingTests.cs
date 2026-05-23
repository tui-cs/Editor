// Claude - claude-opus-4-7

using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Editor.Highlighting;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Text;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Asserts the visual roles used by <see cref="Editor" /> when rendering. Unselected text
///     should blend into the surrounding container (<see cref="VisualRole.Normal" />) — not
///     <see cref="VisualRole.Editable" />, which is the dim/contrast role intended for input
///     widgets like <see cref="Terminal.Gui.Views.TextField" /> and
///     <see cref="Terminal.Gui.Views.TextView" />. Selected text uses
///     <see cref="VisualRole.Active" />.
/// </summary>
public class EditorRenderingTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };
    private static readonly DateTime BaseTime = new (2025, 1, 1, 12, 0, 0);

    [Fact]
    public async Task Unselected_Text_Uses_Normal_Role_Not_Editable ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("Hello"));
        fx.Render ();

        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Attribute editable = fx.Top.Editor.GetAttributeForRole (VisualRole.Editable);

        // Precondition: this test is only meaningful if Normal and Editable differ in the
        // active scheme. If they don't, the visual bug can't manifest and the assertion below
        // would pass spuriously.
        Assert.NotEqual (normal, editable);

        Cell cell = fx.Driver.Contents![0, 0];
        Assert.Equal ("H", cell.Grapheme);
        Assert.Equal (normal, cell.Attribute);
    }

    [Fact]
    public async Task Selected_Text_Uses_Active_Role ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("Hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Render ();

        Attribute active = fx.Top.Editor.GetAttributeForRole (VisualRole.Active);

        Cell cellSelected = fx.Driver.Contents![0, 0];
        Assert.Equal ("H", cellSelected.Grapheme);
        Assert.Equal (active, cellSelected.Attribute);
    }

    [Fact]
    public async Task Unselected_Tail_After_Selection_Uses_Normal_Role ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("Hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Render ();

        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Attribute editable = fx.Top.Editor.GetAttributeForRole (VisualRole.Editable);
        Assert.NotEqual (normal, editable);

        // Cells past the selection (column index >= 2) should be Normal, not Editable.
        Cell tail = fx.Driver.Contents![0, 2];
        Assert.Equal ("l", tail.Grapheme);
        Assert.Equal (normal, tail.Attribute);
    }

    [Fact]
    public async Task LineNumbers_Render_In_LeftPadding ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("alpha\nbeta");
            host.Editor.GutterOptions = GutterOptions.LineNumbers;

            return host;
        });

        fx.Render ();

        Assert.Equal (2, fx.Top.Editor.Padding.Thickness.Left);
        Assert.Equal ("1", fx.Driver.Contents![0, 0].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents[0, 1].Grapheme);
        Assert.Equal ("a", fx.Driver.Contents[0, 2].Grapheme);
        Assert.Equal ("2", fx.Driver.Contents[1, 0].Grapheme);
        Assert.Equal ("b", fx.Driver.Contents[1, 2].Grapheme);
    }

    [Fact]
    public async Task LineNumbers_Follow_Vertical_Scroll ()
    {
        var lines = new string[50];

        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = $"line-{i:00}";
        }

        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new (string.Join ("\n", lines));
            host.Editor.GutterOptions = GutterOptions.LineNumbers;

            return host;
        });

        var offset = 0;

        for (var i = 0; i < 40; i++)
        {
            offset += lines[i].Length + 1;
        }

        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = offset;
        fx.Render ();

        var row = 40 - fx.Top.Editor.Viewport.Y;

        Assert.Equal (3, fx.Top.Editor.Padding.Thickness.Left);
        Assert.Equal ("4", fx.Driver.Contents![row, 0].Grapheme);
        Assert.Equal ("1", fx.Driver.Contents[row, 1].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents[row, 2].Grapheme);
        Assert.Equal ("l", fx.Driver.Contents[row, 3].Grapheme);
    }

    [Fact]
    public async Task LineNumbers_Show_Blank_For_Rows_Past_End_Of_Document ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("only");
            host.Editor.GutterOptions = GutterOptions.LineNumbers;

            return host;
        });

        fx.Render ();

        Assert.Equal ("1", fx.Driver.Contents![0, 0].Grapheme);

        // Row 1 is past the end of a 1-line document — gutter should be blank, not "2".
        Assert.Equal (" ", fx.Driver.Contents![1, 0].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![1, 1].Grapheme);
    }

    [Fact]
    public async Task LineNumbers_Disable_Removes_Gutter_From_Driver ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("alpha\nbeta");
            host.Editor.GutterOptions = GutterOptions.LineNumbers;

            return host;
        });

        fx.Render ();
        Assert.Equal ("1", fx.Driver.Contents![0, 0].Grapheme);

        fx.Top.Editor.GutterOptions = GutterOptions.None;
        fx.Render ();

        // With the gutter gone, content shifts left to column 0.
        Assert.Equal (0, fx.Top.Editor.Padding.Thickness.Left);
        Assert.Equal ("a", fx.Driver.Contents![0, 0].Grapheme);
    }

    [Fact]
    public async Task Syntax_Highlighting_Uses_Xshd_Token_Attributes ()
    {
        const string text = "public class C";

        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (text));

        IHighlightingDefinition? csharp = HighlightingManager.Instance.GetDefinition ("C#");
        Assert.NotNull (csharp);
        fx.Top.Editor.HighlightingDefinition = csharp;
        fx.Render ();

        // With xshd highlighting active, the first cell ("p" from "public") should have
        // a different attribute from the plain Normal attribute, because "public" is a keyword.
        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Cell cell = fx.Driver.Contents![0, 0];
        Assert.Equal ("p", cell.Grapheme);
        Assert.NotEqual (normal, cell.Attribute);
    }

    [Fact]
    public async Task Selection_Overrides_Syntax_Highlighting ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("public class C"));

        fx.Top.Editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Render ();

        Attribute active = fx.Top.Editor.GetAttributeForRole (VisualRole.Active);
        Cell cell = fx.Driver.Contents![0, 0];
        Assert.Equal ("p", cell.Grapheme);
        Assert.Equal (active, cell.Attribute);
    }

    [Fact]
    public async Task Additional_Caret_Selections_Render_With_Active_Role ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd"));
        fx.Top.Editor.SetFocus ();

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (1, 0),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Alt,
                Timestamp = BaseTime
            },
            Direct);

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (3, 2),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport | MouseFlags.Alt,
                Timestamp = BaseTime.AddMilliseconds (20)
            },
            Direct);

        fx.Render ();

        Attribute active = fx.Top.Editor.GetAttributeForRole (VisualRole.Active);

        Assert.Equal ("b", fx.Driver.Contents![1, 1].Grapheme);
        Assert.Equal (active, fx.Driver.Contents[1, 1].Attribute);
        Assert.Equal ("c", fx.Driver.Contents[2, 2].Grapheme);
        Assert.Equal (active, fx.Driver.Contents[2, 2].Attribute);
    }

    [Fact]
    public async Task Tab_Then_Emoji_Renders_At_Correct_Cells ()
    {
        // drawing-overhaul Scenario 3 regression: `\tHello 🌍` must render with the tab expanded
        // to the next tab stop, "Hello" as five normal cells, and the globe emoji at columns
        // [tab-stop + 6, +7] as a two-cell wide grapheme.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("\tHello 🌍"));
        fx.Render ();

        // Default IndentationSize is 4, so the tab expands to columns 0..3.
        Assert.Equal (" ", fx.Driver.Contents![0, 0].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 3].Grapheme);

        Assert.Equal ("H", fx.Driver.Contents![0, 4].Grapheme);
        Assert.Equal ("e", fx.Driver.Contents![0, 5].Grapheme);
        Assert.Equal ("l", fx.Driver.Contents![0, 6].Grapheme);
        Assert.Equal ("l", fx.Driver.Contents![0, 7].Grapheme);
        Assert.Equal ("o", fx.Driver.Contents![0, 8].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 9].Grapheme);

        Assert.Equal ("🌍", fx.Driver.Contents![0, 10].Grapheme);
    }

    [Fact]
    public async Task Tabs_Render_As_Spaces_Using_Default_IndentationSize ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("a\tb"));
        fx.Render ();

        Assert.Equal ("a", fx.Driver.Contents![0, 0].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 1].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 2].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 3].Grapheme);
        Assert.Equal ("b", fx.Driver.Contents![0, 4].Grapheme);
    }

    [Fact]
    public async Task Tabs_Render_As_Spaces_Using_Configured_IndentationSize ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("a\tb"));
        fx.Top.Editor.IndentationSize = 2;
        fx.Render ();

        Assert.Equal ("a", fx.Driver.Contents![0, 0].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 1].Grapheme);
        Assert.Equal ("b", fx.Driver.Contents![0, 2].Grapheme);
    }

    [Fact]
    public async Task ShowTabs_Renders_Glyph_At_First_Tab_Cell ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("a\tb"));
        fx.Top.Editor.ShowTabs = true;
        fx.Render ();

        Assert.Equal ("a", fx.Driver.Contents![0, 0].Grapheme);
        Assert.Equal ("→", fx.Driver.Contents![0, 1].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 2].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 3].Grapheme);
        Assert.Equal ("b", fx.Driver.Contents![0, 4].Grapheme);
    }

    [Fact]
    public async Task Grapheme_Cluster_After_Tab_Renders_At_Expanded_Column ()
    {
        const string emoji = "👩‍💻";

        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ($"a\t{emoji}b"));
        fx.Render ();

        var emojiColumn = 4;
        var nextColumn = emojiColumn + emoji.GetColumns ();

        Assert.Equal (emoji, fx.Driver.Contents![0, emojiColumn].Grapheme);
        Assert.Equal ("b", fx.Driver.Contents![0, nextColumn].Grapheme);
    }

    [Fact]
    public async Task Wide_Grapheme_At_Viewport_Right_Edge_Still_Renders ()
    {
        // "abc💥" with viewport width=4 means visibleEnd=4. The emoji sits at visual
        // column 3, occupying 2 cells (VisualEndColumn=5 > visibleEnd=4). The bug was
        // that TextRunElement.Draw skipped rendering entirely because VisualEndColumn
        // exceeded visibleEnd — leaving column 3 blank.
        const string emoji = "\U0001f4a5"; // 💥 — 2 cells wide

        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ($"abc{emoji}"), 4, 1);
        fx.Render ();

        Assert.Equal ("a", fx.Driver.Contents![0, 0].Grapheme);
        Assert.Equal ("b", fx.Driver.Contents![0, 1].Grapheme);
        Assert.Equal ("c", fx.Driver.Contents![0, 2].Grapheme);
        Assert.Equal (emoji, fx.Driver.Contents![0, 3].Grapheme);
    }

    [Fact]
    public async Task Cursor_Position_After_Tab_Uses_Expanded_Tab_Columns ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("a\tb"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2;
        fx.Render ();

        Assert.Equal (new Point (4, 0), fx.Top.Editor.Cursor.Position);
    }

    [Fact]
    public async Task Cursor_Style_Is_Preserved_When_Position_Updates ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.Cursor = new Cursor { Style = CursorStyle.SteadyUnderline };
        fx.Top.Editor.CaretOffset = 2;
        fx.Render ();

        Assert.Equal (new Point (2, 0), fx.Top.Editor.Cursor.Position);
        Assert.Equal (CursorStyle.SteadyUnderline, fx.Top.Editor.Cursor.Style);
    }

    [Fact]
    public async Task Highlighted_Tokens_Follow_The_Active_Scheme ()
    {
        const string text = "public class C";

        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (text));

        fx.Top.Editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");

        // xshd colors "public" via "Visibility", which XshdRoleMap maps to CodeKeyword.
        // Scheme A explicitly themes CodeKeyword magenta.
        Color black = Color.Black;
        Scheme schemeA = new (new Attribute (Color.White, black))
        {
            CodeKeyword = new Attribute (Color.Magenta, black)
        };
        fx.Top.Editor.SetScheme (schemeA);
        fx.Render ();

        Cell cell = fx.Driver.Contents![0, 0];
        Assert.Equal ("p", cell.Grapheme);
        Assert.Equal (Color.Magenta, cell.Attribute!.Value.Foreground);

        // Swapping the scheme re-renders the same token with the new theme's color.
        Scheme schemeB = new (new Attribute (Color.White, black))
        {
            CodeKeyword = new Attribute (Color.BrightGreen, black)
        };
        fx.Top.Editor.SetScheme (schemeB);
        fx.Render ();

        cell = fx.Driver.Contents![0, 0];
        Assert.Equal (Color.BrightGreen, cell.Attribute!.Value.Foreground);
    }

    [Fact]
    public async Task Editor_Background_Follows_Scheme_Not_Highlighter ()
    {
        const string text = "public class C";

        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (text));

        fx.Top.Editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");
        fx.Render ();

        // The xshd definition's hardcoded background must never reach the cell — the editor's
        // scheme background wins (the #128 fix), whether the token is themed or falls back.
        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Cell cell = fx.Driver.Contents![0, 0];
        Assert.Equal ("p", cell.Grapheme);
        Assert.Equal (normal.Background, cell.Attribute!.Value.Background);
    }

    [Fact]
    public async Task MultiCaret_Renders_Reverse_Blink_Attribute_On_Text ()
    {
        // P1: MultiCaretRenderer must draw AFTER text elements so that the caret
        // cell is not overwritten by the subsequent element.Draw call.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcdef"));

        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;
        fx.Top.Editor.ToggleCaretAt (3); // additional caret on 'd'
        fx.Render ();

        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Attribute caretAttr = new (normal.Foreground, normal.Background, TextStyle.Blink | TextStyle.Reverse);

        // The cell at column 3 ('d') should have the reverse+blink attribute, not the normal one.
        Cell cell = fx.Driver.Contents![0, 3];
        Assert.Equal ("d", cell.Grapheme);
        Assert.Equal (caretAttr, cell.Attribute);
    }

    [Fact]
    public async Task MultiCaret_Renders_At_EndOfLine_Without_Crash ()
    {
        // CR feedback: offset >= segEnd excluded carets at EOL.
        // This test verifies the renderer does not crash when a caret is placed at EOL.
        // The actual attribute verification is in unit tests (IsOffsetInSegment_Correctly_Filters_Offsets).
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));

        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;
        fx.Top.Editor.ToggleCaretAt (3); // EOL position
        fx.Render ();

        // No crash — the renderer successfully processed the EOL caret.
        Cell cell = fx.Driver.Contents![0, 3];
        Assert.Equal (" ", cell.Grapheme);
    }

    [Fact]
    public async Task MultiCaret_Does_Not_Leak_Attribute_To_Adjacent_Cell ()
    {
        // Bug: MultiCaretRenderer.Draw set caretAttr but never restored the normal attribute,
        // causing the next cell to inherit the inverted attribute. Visible as both slashes of
        // "//" appearing highlighted when only one has a caret.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("// comment"));

        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5; // put primary elsewhere (on 'o')
        fx.Top.Editor.ToggleCaretAt (0); // additional caret on first '/'
        fx.Render ();

        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Attribute caretAttr = new (normal.Foreground, normal.Background, TextStyle.Blink | TextStyle.Reverse);

        // Column 0 (first '/') should have the reverse+blink caret attribute.
        Cell cell0 = fx.Driver.Contents![0, 0];
        Assert.Equal ("/", cell0.Grapheme);
        Assert.Equal (caretAttr, cell0.Attribute);

        // Column 1 (second '/') should have the NORMAL attribute, not the caret attribute.
        Cell cell1 = fx.Driver.Contents![0, 1];
        Assert.Equal ("/", cell1.Grapheme);
        Assert.Equal (normal, cell1.Attribute);
    }

    [Fact]
    public async Task MultiCaret_First_Slash_With_SyntaxHighlighting_Only_Highlights_One_Cell ()
    {
        // Bug repro: In ted (with C# syntax highlighting + gutter), Ctrl+clicking the first '/'
        // of "/// summary" highlights ALL three slashes. Clicking the 2nd or 3rd works fine.
        // Test with C# highlighting enabled to match the real ted scenario.
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("/// summary\nint x = 1;");
            host.Editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");

            return host;
        });

        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5; // primary elsewhere (on 's' in "summary")
        fx.Top.Editor.ToggleCaretAt (0); // additional caret on first '/'
        fx.Render ();

        // Grab the Cell buffer attributes for the three slashes.
        Cell cell0 = fx.Driver.Contents![0, 0];
        Cell cell1 = fx.Driver.Contents![0, 1];
        Cell cell2 = fx.Driver.Contents![0, 2];

        Assert.Equal ("/", cell0.Grapheme);
        Assert.Equal ("/", cell1.Grapheme);
        Assert.Equal ("/", cell2.Grapheme);

        // cell0 must have Blink|Reverse (the caret style).
        Assert.True (
            cell0.Attribute!.Value.Style.HasFlag (TextStyle.Blink | TextStyle.Reverse),
            $"Cell 0 should have Blink|Reverse but has Style={cell0.Attribute!.Value.Style}");

        // cell1 and cell2 must NOT have Reverse or Blink.
        Assert.False (
            cell1.Attribute!.Value.Style.HasFlag (TextStyle.Reverse),
            $"Cell 1 should NOT have Reverse but has Style={cell1.Attribute!.Value.Style}");
        Assert.False (
            cell2.Attribute!.Value.Style.HasFlag (TextStyle.Reverse),
            $"Cell 2 should NOT have Reverse but has Style={cell2.Attribute!.Value.Style}");
    }

    [Fact]
    public async Task MultiCaret_WordWrap_No_Duplicate_At_Boundary ()
    {
        // P2: At a wrap boundary, offset == segEnd of one segment AND offset == segStart of the next.
        // With exclusive bound check (>=), the caret should only appear on the second row (segStart),
        // not duplicated on both rows.
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("abcde fghij");
            host.Editor.WordWrap = true;

            return host;
        }, 10, 5);

        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;
        // Place caret at offset 6 which is 'f' — the start of the second wrapped segment.
        fx.Top.Editor.ToggleCaretAt (6);
        fx.Render ();

        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Attribute caretAttr = new (normal.Foreground, normal.Background, TextStyle.Blink | TextStyle.Reverse);

        // Row 1, col 0 should show the caret attribute on 'f'.
        Cell row1FirstCol = fx.Driver.Contents![1, 0];
        Assert.Equal ("f", row1FirstCol.Grapheme);
        Assert.Equal (caretAttr, row1FirstCol.Attribute);
    }
}

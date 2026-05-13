// Claude - claude-opus-4-7

using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Text;
using Terminal.Gui.Editor;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

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
    public async Task UseThemeBackground_True_Preserves_Highlighter_Background ()
    {
        const string text = "public class C";

        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (text));

        fx.Top.Editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");
        fx.Top.Editor.UseThemeBackground = true;
        fx.Render ();

        // With xshd highlighting active, the keyword should get a highlighting color.
        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Cell cell = fx.Driver.Contents![0, 0];
        Assert.Equal ("p", cell.Grapheme);
        Assert.NotEqual (normal, cell.Attribute);
    }

    [Fact]
    public async Task UseThemeBackground_False_Overrides_Highlighter_Background ()
    {
        const string text = "public class C";

        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (text));

        fx.Top.Editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");
        fx.Top.Editor.UseThemeBackground = false;
        fx.Render ();

        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Cell cell = fx.Driver.Contents![0, 0];
        Assert.Equal ("p", cell.Grapheme);

        // The background must match the TG theme's Normal background, not the highlighter's.
        Assert.Equal (normal.Background, cell.Attribute!.Value.Background);
    }

    [Fact]
    public async Task UseThemeBackground_Defaults_To_True ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("Hello"));

        Assert.True (fx.Top.Editor.UseThemeBackground);
    }
}

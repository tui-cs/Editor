// Claude - claude-opus-4-7

using Terminal.Gui.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using TextMateSharp.Grammars;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Asserts the visual roles used by <see cref="Views.Editor" /> when rendering. Unselected text
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
        await using AppFixture<EditorTestHost> fx = new (() => new ("Hello"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new ("Hello"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new ("Hello"));
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
            host.Editor.ShowLineNumbers = true;

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
        string[] lines = new string[50];

        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = $"line-{i:00}";
        }

        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new (string.Join ("\n", lines));
            host.Editor.ShowLineNumbers = true;

            return host;
        });

        int offset = 0;

        for (int i = 0; i < 40; i++)
        {
            offset += lines[i].Length + 1;
        }

        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = offset;
        fx.Render ();

        int row = 40 - fx.Top.Editor.Viewport.Y;

        Assert.Equal (3, fx.Top.Editor.Padding.Thickness.Left);
        Assert.Equal ("4", fx.Driver.Contents![row, 0].Grapheme);
        Assert.Equal ("1", fx.Driver.Contents[row, 1].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents[row, 2].Grapheme);
        Assert.Equal ("l", fx.Driver.Contents[row, 3].Grapheme);
    }

    [Fact]
    public async Task Syntax_Highlighting_Uses_TextMate_Token_Attributes ()
    {
        const string text = "public class C";

        await using AppFixture<EditorTestHost> fx = new (() => new (text));
        // Editor.SyntaxHighlighter is [Obsolete] (issue #32); these tests still exercise it
        // because it's the live behavior until the visual-line pipeline lands (issue #28).
#pragma warning disable CS0618 // Type or member is obsolete
        fx.Top.Editor.SyntaxHighlighter = new TextMateSyntaxHighlighter (ThemeName.DarkPlus);
#pragma warning restore CS0618 // Type or member is obsolete
        fx.Render ();

        TextMateSyntaxHighlighter highlighter = new (ThemeName.DarkPlus);
        Attribute expected = highlighter.Highlight (text, "csharp")[0].Attribute!.Value;

        Cell cell = fx.Driver.Contents![0, 0];
        Assert.Equal ("p", cell.Grapheme);
        Assert.Equal (expected, cell.Attribute);
        Assert.NotEqual (fx.Top.Editor.GetAttributeForRole (VisualRole.Normal), cell.Attribute);
    }

    [Fact]
    public async Task Selection_Overrides_Syntax_Highlighting ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("public class C"));
#pragma warning disable CS0618 // Type or member is obsolete — see note in Syntax_Highlighting_Uses_TextMate_Token_Attributes.
        fx.Top.Editor.SyntaxHighlighter = new TextMateSyntaxHighlighter (ThemeName.DarkPlus);
#pragma warning restore CS0618 // Type or member is obsolete
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
    public async Task Tabs_Render_As_Spaces_Using_Default_IndentationSize ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("a\tb"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new ("a\tb"));
        fx.Top.Editor.IndentationSize = 2;
        fx.Render ();

        Assert.Equal ("a", fx.Driver.Contents![0, 0].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 1].Grapheme);
        Assert.Equal ("b", fx.Driver.Contents![0, 2].Grapheme);
    }

    [Fact]
    public async Task Tabs_Render_With_Glyph_When_ShowTabs_Is_True ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("a\tb"));
        fx.Top.Editor.ShowTabs = true;
        fx.Render ();

        Assert.Equal ("a", fx.Driver.Contents![0, 0].Grapheme);
        Assert.Equal ("→", fx.Driver.Contents![0, 1].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 2].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 3].Grapheme);
        Assert.Equal ("b", fx.Driver.Contents![0, 4].Grapheme);
    }

    [Fact]
    public async Task Grapheme_Cluster_Renders_Correctly_On_Line_With_Tabs ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("a\t👨‍👩‍👧‍👦b"));
        fx.Render ();

        Assert.Equal ("👨‍👩‍👧‍👦", fx.Driver.Contents![0, 4].Grapheme);
        Assert.Equal ("b", fx.Driver.Contents![0, 6].Grapheme);
    }

    [Fact]
    public async Task Cursor_Position_After_Tab_Uses_Expanded_Tab_Columns ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("a\tb"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2;
        fx.Render ();

        Assert.Equal (new (4, 0), fx.Top.Editor.Cursor.Position);
    }
}

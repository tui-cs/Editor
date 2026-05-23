// Claude - claude-opus-4-7

using Terminal.Gui.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Editor.Highlighting;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Dogfoods <see cref="AnsiSnapshot" />: render a user action, snapshot the screen as pure
///     ANSI. The recorded <c>__snapshots__/*.ans</c> can be <c>cat</c>'d to see the exact look
///     (colors, selection highlight, multi-caret cells) without running the app interactively.
/// </summary>
public class EditorSnapshotTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    // A small viewport keeps the golden compact and the `cat` reproduction tidy.
    private const int W = 12;
    private const int H = 4;

    [Fact]
    public async Task Plain_Document_Renders ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nefgh\nijkl"), W, H);
        fx.Top.Editor.SetFocus ();
        fx.Render ();

        AnsiSnapshot.Verify (fx.Driver, nameof (Plain_Document_Renders));
    }

    [Fact]
    public async Task Keyboard_Column_Selection_Highlights_Each_Row ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd"), W, H);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (Key.CursorRight.WithCtrl.WithShift.WithAlt, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithCtrl.WithShift.WithAlt, Direct);
        fx.Injector.InjectKey (Key.CursorDown.WithCtrl.WithShift.WithAlt, Direct);
        fx.Render ();

        // Snapshot proves "bc" is the active-role selection on both the primary row and the
        // additional-caret row — the exact look the PR adds, verifiable without eyeballing.
        AnsiSnapshot.Verify (fx.Driver, nameof (Keyboard_Column_Selection_Highlights_Each_Row));
    }

    [Fact]
    public async Task Snapshot_Render_Is_Deterministic ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd"), W, H);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (Key.CursorRight.WithCtrl.WithShift.WithAlt, Direct);
        fx.Injector.InjectKey (Key.CursorDown.WithCtrl.WithShift.WithAlt, Direct);
        fx.Render ();
        var first = fx.Driver.ToAnsi ();

        fx.Render ();
        var second = fx.Driver.ToAnsi ();

        // Guard: ToAnsi must be a pure function of buffer state, or goldens flake.
        Assert.Equal (first, second);
    }

    [Fact]
    public async Task Markdown_Headings_And_Links_Use_Themed_Roles_Snapshot ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
            new EditorTestHost ("# Heading\n[link](https://example.com)"));
        fx.Top.Editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("MarkDown");
        fx.Top.Editor.SetScheme (new Scheme (new Attribute (Color.White, Color.Black))
        {
            CodeKeyword = new Attribute (Color.BrightRed, Color.Black),
            CodeIdentifier = new Attribute (Color.BrightCyan, Color.Black)
        });

        fx.Render ();

        AnsiSnapshot.Verify (fx.Driver, nameof (Markdown_Headings_And_Links_Use_Themed_Roles_Snapshot));
    }
}

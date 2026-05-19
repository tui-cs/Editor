// Claude - claude-opus-4-7

using System.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Regression cover for the Codex review P1 on <c>Editor.Indentation.cs</c>:
///     <c>InsertTab()</c> / <c>Unindent()</c> short-circuited to the multi-caret path the
///     moment <see cref="Editor.HasMultipleCarets" /> was true — <em>before</em> the
///     multi-line-selection branch — so a multi-line selection coexisting with an extra
///     caret was silently replaced by a single tab (data loss) instead of block-indented.
///     Encodes the <i>Tab with a multi-line selection plus an extra caret</i> scenario from
///     <c>specs/vertical-multi-caret/spec.md</c>.
/// </summary>
public class EditorMultiCaretIndentTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };
    private static readonly DateTime BaseTime = new (2025, 1, 1, 12, 0, 0);

    [Fact]
    public async Task Tab_MultilineSelection_Plus_PointCaret_BlockIndents_Does_Not_Delete ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("alpha\nbeta\ngamma\ndelta"));
        fx.Top.Editor.SetFocus ();

        // Primary selection covers lines 1-3 (alpha/beta/gamma); an additional point
        // caret (no selection) sits on line 4 (delta).
        fx.Top.Editor.SelectRange (0, "alpha\nbeta\ngamma".Length);
        fx.Top.Editor.ToggleCaretAt ("alpha\nbeta\ngamma\n".Length);
        Assert.True (fx.Top.Editor.HasMultipleCarets);

        fx.Injector.InjectKey (Key.Tab, Direct);

        // Lines 1-3 are block-indented AND the line-4 caret gets a tab. The selected
        // block must NOT be replaced by a single tab.
        Assert.Equal ("\talpha\n\tbeta\n\tgamma\n\tdelta", fx.Top.Editor.Document!.Text);

        // One undo step reverses every caret's effect.
        fx.Top.Editor.Document.UndoStack.Undo ();
        Assert.Equal ("alpha\nbeta\ngamma\ndelta", fx.Top.Editor.Document.Text);
    }

    [Fact]
    public async Task ShiftTab_MultilineSelection_Plus_PointCaret_BlockUnindents ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("\talpha\n\tbeta\n\tgamma\n\tdelta"));
        fx.Top.Editor.SetFocus ();

        // Primary selection covers indented lines 1-3; additional point caret on line 4.
        fx.Top.Editor.SelectRange (0, "\talpha\n\tbeta\n\tgamma\n".Length);
        fx.Top.Editor.ToggleCaretAt ("\talpha\n\tbeta\n\tgamma\n\t".Length);
        Assert.True (fx.Top.Editor.HasMultipleCarets);

        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        // Lines 1-3 block-unindent AND line 4 loses its leading indent — one undo step.
        Assert.Equal ("alpha\nbeta\ngamma\ndelta", fx.Top.Editor.Document!.Text);

        fx.Top.Editor.Document.UndoStack.Undo ();
        Assert.Equal ("\talpha\n\tbeta\n\tgamma\n\tdelta", fx.Top.Editor.Document.Text);
    }

    [Fact]
    public async Task Tab_ColumnSelection_Preserves_PerCaret_Selections_After_BlockIndent ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abcd\nabcd\nabcd"));
        fx.Top.Editor.SetFocus ();

        InjectAltDrag (fx, new (1, 0), new (3, 2));
        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("\tabcd\n\tabcd\n\tabcd", fx.Top.Editor.Document!.Text);

        fx.Injector.InjectKey (Key.X, Direct);

        Assert.Equal ("\taxd\n\taxd\n\taxd", fx.Top.Editor.Document.Text);
    }

    private static void InjectAltDrag (AppFixture<EditorTestHost> fx, Point press, Point drag)
    {
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = press,
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Alt,
                Timestamp = BaseTime
            },
            Direct);

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = drag,
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport | MouseFlags.Alt,
                Timestamp = BaseTime.AddMilliseconds (20)
            },
            Direct);
    }
}

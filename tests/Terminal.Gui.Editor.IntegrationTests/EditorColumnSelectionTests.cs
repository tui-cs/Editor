// Claude - claude-opus-4-7

using System.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Vets PR #142 (column selection for vertical multi-caret) end-to-end through the real
///     mouse/keyboard input pipeline. Each test drives a gesture, then both asserts the
///     document/caret semantics and snapshots the rendered ANSI so the <i>look</i>
///     (per-row highlight, additional-caret cells, clamping) is locked without eyeballing.
///     <c>cat __snapshots__/&lt;name&gt;.ans</c> reproduces the exact screen.
/// </summary>
public class EditorColumnSelectionTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    private const int W = 10;

    private static Key CsaW (Key baseKey) { return baseKey.WithCtrl.WithShift.WithAlt; }

    // ---- Mouse: Alt+drag ------------------------------------------------------------------

    [Fact]
    public async Task AltDrag_Column_Down_Selects_Each_Row ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd\nabcd"), W, 5);
        fx.Top.Editor.SetFocus ();

        Inject.AltDrag (fx, new Point (1, 0), new Point (3, 2));
        fx.Render ();

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.True (fx.Top.Editor.HasMultipleCarets);
        Assert.Equal (3, fx.Top.Editor.CaretOffset); // row0 col3
        Assert.Equal (2, fx.Top.Editor.AdditionalCaretOffsets.Count);
        Assert.Contains (8, fx.Top.Editor.AdditionalCaretOffsets); // row1 col3
        Assert.Contains (13, fx.Top.Editor.AdditionalCaretOffsets); // row2 col3

        AnsiSnapshot.Verify (fx.Driver, nameof (AltDrag_Column_Down_Selects_Each_Row));
    }

    [Fact]
    public async Task AltDrag_Reversed_Puts_Caret_Left_Of_Anchor ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd"), W, 4);
        fx.Top.Editor.SetFocus ();

        // Press at col 3, drag left to col 1 (and down two rows).
        Inject.AltDrag (fx, new Point (3, 0), new Point (1, 2));
        fx.Render ();

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (1, fx.Top.Editor.CaretOffset); // caret on the leftward edge
        Assert.Equal (2, fx.Top.Editor.AdditionalCaretOffsets.Count);

        AnsiSnapshot.Verify (fx.Driver, nameof (AltDrag_Reversed_Puts_Caret_Left_Of_Anchor));
    }

    [Fact]
    public async Task AltDrag_Zero_Horizontal_Extent_Makes_Carets_Not_Selection ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd\nabcd"), W, 5);
        fx.Top.Editor.SetFocus ();

        Inject.AltDrag (fx, new Point (2, 0), new Point (2, 3));
        fx.Render ();

        Assert.False (fx.Top.Editor.HasSelection); // pure column of carets
        Assert.True (fx.Top.Editor.HasMultipleCarets);
        Assert.Equal (3, fx.Top.Editor.AdditionalCaretOffsets.Count);

        AnsiSnapshot.Verify (fx.Driver, nameof (AltDrag_Zero_Horizontal_Extent_Makes_Carets_Not_Selection));
    }

    [Fact]
    public async Task AltDrag_Over_Short_Line_Clamps_Without_Padding ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\na\nabcd"), W, 4);
        fx.Top.Editor.SetFocus ();

        // Drag a wide column (col 1 -> col 4) across a 1-char middle line.
        Inject.AltDrag (fx, new Point (1, 0), new Point (4, 2));
        fx.Render ();

        // The short row's selection clamps to its real end — no padding is written when typed.
        fx.Injector.InjectKey (Key.X, Direct);
        fx.Render ();

        Assert.Equal ("ax\nax\nax", fx.Top.Editor.Document!.Text);

        // Snapshot is post-type: the short row is "ax", not "a   x" — visual proof of no padding.
        AnsiSnapshot.Verify (fx.Driver, nameof (AltDrag_Over_Short_Line_Clamps_Without_Padding));
    }

    [Fact]
    public async Task AltDrag_MultiWaypoint_Rebuilds_From_Final_Point ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd\nabcd"), W, 5);
        fx.Top.Editor.SetFocus ();

        // Wander through (3,1) then settle at (2,3); end state must equal a single (1,0)->(2,3).
        Inject.AltDrag (fx, new Point (1, 0), new Point (3, 1), new Point (2, 3));
        fx.Render ();

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (2, fx.Top.Editor.CaretOffset); // row0 col2 (final), not col3
        Assert.Equal (3, fx.Top.Editor.AdditionalCaretOffsets.Count); // rows 1..3

        AnsiSnapshot.Verify (fx.Driver, nameof (AltDrag_MultiWaypoint_Rebuilds_From_Final_Point));
    }

    [Fact]
    public async Task AltDrag_Then_Plain_Click_Collapses_To_Single_Caret ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd"), W, 4);
        fx.Top.Editor.SetFocus ();

        Inject.AltDrag (fx, new Point (1, 0), new Point (3, 2));
        Assert.True (fx.Top.Editor.HasMultipleCarets);

        Inject.Click (fx, new Point (0, 0));
        fx.Render ();

        Assert.False (fx.Top.Editor.HasMultipleCarets);
        Assert.False (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);

        AnsiSnapshot.Verify (fx.Driver, nameof (AltDrag_Then_Plain_Click_Collapses_To_Single_Caret));
    }

    // ---- Keyboard: Ctrl+Shift+Alt --------------------------------------------------------

    [Fact]
    public async Task Keyboard_Right_Then_Down_Builds_Column ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd"), W, 4);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (CsaW (Key.CursorRight), Direct);
        fx.Injector.InjectKey (CsaW (Key.CursorRight), Direct);
        fx.Injector.InjectKey (CsaW (Key.CursorDown), Direct);
        fx.Render ();

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.True (fx.Top.Editor.HasMultipleCarets);
        Assert.Single (fx.Top.Editor.AdditionalCaretOffsets);

        AnsiSnapshot.Verify (fx.Driver, nameof (Keyboard_Right_Then_Down_Builds_Column));
    }

    [Fact]
    public async Task Keyboard_PageDown_Extends_By_Viewport ()
    {
        await using AppFixture<EditorTestHost> fx =
            new (() => new EditorTestHost ("abcd\nabcd\nabcd\nabcd\nabcd\nabcd\nabcd\nabcd"), W, 4);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (CsaW (Key.CursorRight), Direct);
        fx.Injector.InjectKey (CsaW (Key.PageDown), Direct);
        fx.Render ();

        // PageDown extends by one viewport height; the column spans more rows than are visible.
        Assert.True (fx.Top.Editor.HasMultipleCarets);
        Assert.Equal (fx.Top.Editor.Viewport.Height, fx.Top.Editor.AdditionalCaretOffsets.Count);

        AnsiSnapshot.Verify (fx.Driver, nameof (Keyboard_PageDown_Extends_By_Viewport));
    }

    [Fact]
    public async Task Keyboard_Left_Past_Anchor_Reverses_Selection ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcde\nabcde\nabcde"), W, 4);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2; // col 2

        fx.Injector.InjectKey (CsaW (Key.CursorRight), Direct); // active col -> 3
        fx.Injector.InjectKey (CsaW (Key.CursorLeft), Direct);
        fx.Injector.InjectKey (CsaW (Key.CursorLeft), Direct);
        fx.Injector.InjectKey (CsaW (Key.CursorLeft), Direct); // active col -> 0, left of anchor
        fx.Render ();

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);

        AnsiSnapshot.Verify (fx.Driver, nameof (Keyboard_Left_Past_Anchor_Reverses_Selection));
    }

    [Fact]
    public async Task Keyboard_Column_Then_Esc_Collapses_Carets ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd"), W, 4);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (CsaW (Key.CursorRight), Direct);
        fx.Injector.InjectKey (CsaW (Key.CursorDown), Direct);
        Assert.True (fx.Top.Editor.HasMultipleCarets);

        fx.Injector.InjectKey (Key.Esc, Direct);
        fx.Render ();

        Assert.False (fx.Top.Editor.HasMultipleCarets);
        Assert.False (fx.Top.Editor.HasSelection);

        AnsiSnapshot.Verify (fx.Driver, nameof (Keyboard_Column_Then_Esc_Collapses_Carets));
    }

    // ---- Single undo scope ----------------------------------------------------------------

    [Fact]
    public async Task Column_Type_Then_Single_Undo_Restores_All_Rows ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcd\nabcd\nabcd"), W, 4);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (CsaW (Key.CursorRight), Direct);
        fx.Injector.InjectKey (CsaW (Key.CursorRight), Direct);
        fx.Injector.InjectKey (CsaW (Key.CursorDown), Direct);
        fx.Injector.InjectKey (CsaW (Key.CursorDown), Direct);
        fx.Injector.InjectKey (Key.X, Direct);

        Assert.Equal ("axd\naxd\naxd", fx.Top.Editor.Document!.Text);

        // The whole multi-row replace is one update scope — a single Undo restores everything.
        fx.Top.Editor.Document.UndoStack.Undo ();

        Assert.Equal ("abcd\nabcd\nabcd", fx.Top.Editor.Document.Text);
    }
}

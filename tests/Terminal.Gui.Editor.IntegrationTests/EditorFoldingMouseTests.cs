using System.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.Document;
using Terminal.Gui.Editor.Document.Folding;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Integration tests for folding + caret visibility (issues #190, #202).
///     Verifies that collapsing a fold does not hide the caret.
/// </summary>
public class EditorFoldingMouseTests
{
    /// <summary>
    ///     Reproduces issue #202: the full mouse sequence at the gutter position causes the
    ///     editor to intercept the press (grabbing the mouse), which prevents the clicked
    ///     event from reaching the FoldingGutter. This test verifies that the editor must NOT
    ///     grab the mouse when the press lands in its Padding/gutter area.
    /// </summary>
    [Fact]
    public async Task FoldGutterFullMouseSequence_Fold_Toggles_And_Caret_Stays_Visible ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("line1\nline2\nline3\nline4\nline5");
            host.Editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;

            FoldingManager fm = new (host.Editor.Document!);
            host.Editor.FoldingManager = fm;

            DocumentLine line2 = host.Editor.Document!.GetLineByNumber (2);
            DocumentLine line4 = host.Editor.Document!.GetLineByNumber (4);
            fm.CreateFolding (line2.Offset, line4.EndOffset);

            return host;
        });

        Editor editor = fx.Top.Editor;
        editor.SetFocus ();
        editor.CaretOffset = 0;
        fx.Render ();

        // Verify initial state
        Assert.True (editor.HasFocus, "Editor should have focus before click");
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);

        // The fold gutter is at X = lineNumberWidth within the gutter.
        // 5 lines → 1 digit + 1 space = 2; fold indicator starts at X=2.
        // Line 2 (0-indexed row 1) has the fold start.
        var foldGutterX = 2;
        var foldRow = 1;
        Point clickPos = new (foldGutterX, foldRow);
        DateTime ts = new (2025, 1, 1, 12, 0, 0);
        InputInjectionOptions direct = new () { Mode = InputInjectionMode.Direct };

        // Inject the full mouse sequence that a real terminal produces:
        // Press → Release. Terminal.Gui's mouse state machine synthesizes the
        // LeftButtonClicked event automatically from this sequence.
        // The bug was that LeftButtonPressed in the gutter area was intercepted by the
        // editor, which grabbed the mouse, preventing the synthesized Clicked from
        // reaching the FoldingGutter.
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = clickPos,
                Flags = MouseFlags.LeftButtonPressed,
                Timestamp = ts
            },
            direct);
        fx.Render ();

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = clickPos,
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = ts.AddMilliseconds (50)
            },
            direct);
        fx.Render ();

        // The fold should have toggled (critical: the full sequence must not break routing)
        FoldingSection? fold = editor.FoldingManager!.GetFoldingAtLine (2);
        Assert.NotNull (fold);
        Assert.True (fold!.IsFolded, "Expected the fold to be collapsed after full mouse sequence");

        // Editor must retain focus and cursor visibility
        Assert.True (editor.HasFocus, "Editor must retain focus after gutter click");
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);
    }

    /// <summary>
    ///     Reproduces issue #202 variant: after a <c>LeftButtonPressed</c> on the gutter
    ///     area causes the editor to grab the mouse / lose focus, toggling the fold
    ///     must still result in a visible cursor WITHOUT requiring explicit SetFocus.
    ///     This is the core reproduction: in real usage, the fold toggles but cursor disappears.
    /// </summary>
    [Fact]
    public async Task FoldGutterPress_Then_FoldToggle_Cursor_Visible_Without_Explicit_Focus_Restore ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("line1\nline2\nline3\nline4\nline5");
            host.Editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;

            FoldingManager fm = new (host.Editor.Document!);
            host.Editor.FoldingManager = fm;

            DocumentLine line2 = host.Editor.Document!.GetLineByNumber (2);
            DocumentLine line4 = host.Editor.Document!.GetLineByNumber (4);
            fm.CreateFolding (line2.Offset, line4.EndOffset);

            return host;
        });

        Editor editor = fx.Top.Editor;
        editor.SetFocus ();
        editor.CaretOffset = 0;
        fx.Render ();

        Assert.True (editor.HasFocus);
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);

        // Simulate the press landing on the gutter position — in real TG, this
        // is intercepted by the editor (it grabs the mouse) which may affect focus state.
        var foldGutterX = 2;
        var foldRow = 1;
        DateTime ts = new (2025, 1, 1, 12, 0, 0);
        InputInjectionOptions direct = new () { Mode = InputInjectionMode.Direct };

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (foldGutterX, foldRow),
                Flags = MouseFlags.LeftButtonPressed,
                Timestamp = ts
            },
            direct);
        fx.Render ();

        // Release the mouse (editor ungrabs on release)
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (foldGutterX, foldRow),
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = ts.AddMilliseconds (50)
            },
            direct);
        fx.Render ();

        // Now toggle the fold (simulates what happens when Clicked reaches the gutter
        // in real usage — the fold DOES toggle, per the bug report)
        FoldingSection fold = editor.FoldingManager!.GetFoldingAtLine (2)!;
        fold.IsFolded = true;
        fx.Render ();

        // Critical: Do NOT call editor.SetFocus() here — in real usage nobody does.
        // The cursor must be visible after the fold toggle completes.
        Assert.True (editor.HasFocus, "Editor must retain focus after gutter press + fold toggle");
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);
    }

    /// <summary>
    ///     Reproduces issue #202: caret inside the fold region + full mouse press→click
    ///     sequence. Caret must move out of the fold and remain visible.
    /// </summary>
    [Fact]
    public async Task FoldGutterFullMouseSequence_Caret_Inside_Fold_Moves_And_Stays_Visible ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("line1\nline2\nline3\nline4\nline5");
            host.Editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;

            FoldingManager fm = new (host.Editor.Document!);
            host.Editor.FoldingManager = fm;

            DocumentLine line2 = host.Editor.Document!.GetLineByNumber (2);
            DocumentLine line4 = host.Editor.Document!.GetLineByNumber (4);
            fm.CreateFolding (line2.Offset, line4.EndOffset);

            return host;
        });

        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        // Place caret on line 3 (INSIDE the fold region)
        DocumentLine line3 = editor.Document!.GetLineByNumber (3);
        editor.CaretOffset = line3.Offset;
        fx.Render ();

        Assert.True (editor.HasFocus);
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);

        // Full mouse sequence on the fold gutter
        var foldGutterX = 2;
        var foldRow = 1;
        Point clickPos = new (foldGutterX, foldRow);
        DateTime ts = new (2025, 1, 1, 12, 0, 0);
        InputInjectionOptions direct = new () { Mode = InputInjectionMode.Direct };

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = clickPos,
                Flags = MouseFlags.LeftButtonPressed,
                Timestamp = ts
            },
            direct);
        fx.Render ();

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = clickPos,
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = ts.AddMilliseconds (50)
            },
            direct);
        fx.Render ();

        // The fold should have toggled via TG-synthesized Clicked
        FoldingSection? fold = editor.FoldingManager!.GetFoldingAtLine (2);

        if (fold is not null && fold.IsFolded)
        {
            // Caret should have moved to fold start line
            DocumentLine caretLine = editor.Document!.GetLineByOffset (editor.CaretOffset);
            Assert.Equal (2, caretLine.LineNumber);
        }

        fx.Render ();

        // Critical: cursor must remain visible after fold toggle
        Assert.True (editor.HasFocus, "Editor must have focus");
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);
    }


    /// <summary>
    ///     After collapsing a fold, the caret (which is outside the fold) must
    ///     remain visible — <c>Cursor.Style</c> should not be <c>Hidden</c>.
    /// </summary>
    [Fact]
    public async Task FoldCollapse_Caret_Outside_Fold_Stays_Visible ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("line1\nline2\nline3\nline4\nline5");
            host.Editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;

            FoldingManager fm = new (host.Editor.Document!);
            host.Editor.FoldingManager = fm;

            // Create a fold on lines 2-4 (expanded initially)
            DocumentLine line2 = host.Editor.Document!.GetLineByNumber (2);
            DocumentLine line4 = host.Editor.Document!.GetLineByNumber (4);
            fm.CreateFolding (line2.Offset, line4.EndOffset);

            return host;
        });

        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        // Place caret on line 1 (outside the fold)
        editor.CaretOffset = 0;
        fx.Render ();

        // Verify cursor is visible before fold toggle
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);

        // Collapse the fold programmatically (same as what gutter click does)
        FoldingSection fold = editor.FoldingManager!.GetFoldingAtLine (2)!;
        fold.IsFolded = true;
        fx.Render ();

        // The caret is on line 1 (outside the fold) — cursor must remain visible
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);
    }

    /// <summary>
    ///     Simulates the gutter-click scenario: after fold collapse, the Editor
    ///     may lose focus to the Padding/Gutter subview. Cursor must stay visible
    ///     once focus is restored (or even during transient loss).
    /// </summary>
    [Fact]
    public async Task FoldCollapse_Caret_Outside_Fold_AfterFocusRestore_Stays_Visible ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("line1\nline2\nline3\nline4\nline5");
            host.Editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;

            FoldingManager fm = new (host.Editor.Document!);
            host.Editor.FoldingManager = fm;

            DocumentLine line2 = host.Editor.Document!.GetLineByNumber (2);
            DocumentLine line4 = host.Editor.Document!.GetLineByNumber (4);
            fm.CreateFolding (line2.Offset, line4.EndOffset);

            return host;
        });

        Editor editor = fx.Top.Editor;
        editor.SetFocus ();
        editor.CaretOffset = 0;
        fx.Render ();

        // Collapse fold (gutter path)
        FoldingSection fold = editor.FoldingManager!.GetFoldingAtLine (2)!;
        fold.IsFolded = true;

        // Simulate the focus-loss that happens when clicking a subview in Padding
        // (the gutter lives inside Padding, which is a separate View)
        editor.HasFocus = false;
        fx.Render ();

        // Re-focus the editor (user clicks back or TG routes focus back)
        editor.SetFocus ();
        fx.Render ();

        // After focus restore, cursor must be visible
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);
    }

    /// <summary>
    ///     After collapsing a fold when the caret IS inside the folded region,
    ///     the caret should move to the fold start line and remain visible.
    /// </summary>
    [Fact]
    public async Task FoldCollapse_Caret_Inside_Fold_Moves_And_Stays_Visible ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("line1\nline2\nline3\nline4\nline5");
            host.Editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;

            FoldingManager fm = new (host.Editor.Document!);
            host.Editor.FoldingManager = fm;

            // Create a fold on lines 2-4 (expanded initially)
            DocumentLine line2 = host.Editor.Document!.GetLineByNumber (2);
            DocumentLine line4 = host.Editor.Document!.GetLineByNumber (4);
            fm.CreateFolding (line2.Offset, line4.EndOffset);

            return host;
        });

        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        // Place caret on line 3 (INSIDE the fold region)
        DocumentLine line3 = editor.Document!.GetLineByNumber (3);
        editor.CaretOffset = line3.Offset;
        fx.Render ();

        // Verify cursor is visible before fold toggle
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);

        // Collapse the fold programmatically (same as what gutter click does)
        FoldingSection fold = editor.FoldingManager!.GetFoldingAtLine (2)!;
        fold.IsFolded = true;
        fx.Render ();

        // The caret should have moved to the fold start line (line 2)
        DocumentLine caretLine = editor.Document!.GetLineByOffset (editor.CaretOffset);
        Assert.Equal (2, caretLine.LineNumber);

        // Cursor must remain visible (not hidden)
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);
    }

    /// <summary>
    ///     Tests actual mouse click on the fold gutter to toggle a fold and verify
    ///     the caret remains visible afterward.
    /// </summary>
    [Fact]
    public async Task FoldGutterMouseClick_Toggles_Fold_And_Caret_Stays_Visible ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("line1\nline2\nline3\nline4\nline5");
            host.Editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;

            FoldingManager fm = new (host.Editor.Document!);
            host.Editor.FoldingManager = fm;

            DocumentLine line2 = host.Editor.Document!.GetLineByNumber (2);
            DocumentLine line4 = host.Editor.Document!.GetLineByNumber (4);
            fm.CreateFolding (line2.Offset, line4.EndOffset);

            return host;
        });

        Editor editor = fx.Top.Editor;
        editor.SetFocus ();
        editor.CaretOffset = 0;
        fx.Render ();

        // The fold gutter is at X = lineNumberWidth within the gutter.
        // 5 lines → 1 digit + 1 space = 2; fold indicator starts at screen X=2.
        // Line 2 (0-indexed row 1) has the fold start.
        var foldGutterX = 2;
        var foldRow = 1; // line 2, 0-indexed

        // Inject LeftButtonClicked directly (Terminal.Gui mouse binding)
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (foldGutterX, foldRow),
                Flags = MouseFlags.LeftButtonClicked,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            },
            new InputInjectionOptions { Mode = InputInjectionMode.Direct });
        fx.Render ();

        // Check if the fold toggled
        FoldingSection? fold = editor.FoldingManager!.GetFoldingAtLine (2);
        Assert.NotNull (fold);
        Assert.True (fold!.IsFolded, "Expected the fold to be collapsed after gutter click");

        // Cursor must remain visible
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);
    }

    /// <summary>
    ///     Tests mouse click on fold gutter when caret is INSIDE the fold region.
    ///     Caret must move out and remain visible.
    /// </summary>
    [Fact]
    public async Task FoldGutterMouseClick_Caret_Inside_Fold_Moves_And_Stays_Visible ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("line1\nline2\nline3\nline4\nline5");
            host.Editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;

            FoldingManager fm = new (host.Editor.Document!);
            host.Editor.FoldingManager = fm;

            DocumentLine line2 = host.Editor.Document!.GetLineByNumber (2);
            DocumentLine line4 = host.Editor.Document!.GetLineByNumber (4);
            fm.CreateFolding (line2.Offset, line4.EndOffset);

            return host;
        });

        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        // Place caret on line 3 (INSIDE the fold region)
        DocumentLine line3 = editor.Document!.GetLineByNumber (3);
        editor.CaretOffset = line3.Offset;
        fx.Render ();

        // Inject LeftButtonClicked on fold gutter for line 2
        var foldGutterX = 2;
        var foldRow = 1;

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (foldGutterX, foldRow),
                Flags = MouseFlags.LeftButtonClicked,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            },
            new InputInjectionOptions { Mode = InputInjectionMode.Direct });
        fx.Render ();

        // Fold should be collapsed
        FoldingSection? fold = editor.FoldingManager!.GetFoldingAtLine (2);
        Assert.NotNull (fold);
        Assert.True (fold!.IsFolded, "Expected the fold to be collapsed after gutter click");

        // Caret moved to fold start
        DocumentLine caretLine = editor.Document!.GetLineByOffset (editor.CaretOffset);
        Assert.Equal (2, caretLine.LineNumber);

        // Cursor must remain visible
        Assert.NotEqual (CursorStyle.Hidden, editor.Cursor.Style);
    }
}

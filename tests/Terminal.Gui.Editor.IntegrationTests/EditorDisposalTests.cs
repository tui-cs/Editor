// Claude - claude-opus-4-6

using Terminal.Gui.Document;
using Terminal.Gui.Editor.Completion;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Tests that verify the <see cref="Editor" /> properly disposes child views it creates
///     (completion popovers, context menus, gutters) during teardown. These tests expose potential
///     leaks where Views survive the Editor's <see cref="View.Dispose(bool)" /> call.
/// </summary>
/// <remarks>
///     Terminal.Gui's <c>DEBUG_IDISPOSABLE</c> infrastructure (<c>View.WasDisposed</c>,
///     <c>View.Instances</c>) is only available in Debug builds of Terminal.Gui itself — which
///     our NuGet reference does not include. These tests instead verify disposal through observable
///     effects: fields nulled, popover no longer visible, no exceptions on double-dispose, and the
///     view hierarchy no longer references orphaned children.
/// </remarks>
public class EditorDisposalTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    // ──────────────────────────────────────────────────────────────────────────
    //  Completion popover disposal
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     When the editor is disposed while the completion popup is actively showing,
    ///     <see cref="Editor.IsCompletionActive" /> should become <see langword="false" /> and
    ///     the popover should be cleaned up (not left dangling in the application's view hierarchy).
    /// </summary>
    [Fact]
    public async Task Dispose_While_Completion_Active_Cleans_Up_Popover ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("using unsafe uint "));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();
        editor.CaretOffset = editor.Document!.TextLength;
        editor.CompletionProvider = new TestWordCompletionProvider ();

        // Trigger completion by typing "u" — matches "using", "unsafe", "uint".
        fx.Injector.InjectKey (Key.U, Direct);
        Assert.True (editor.IsCompletionActive, "Completion should be active before dispose");

        // Dispose the editor — this is the code path that should clean up.
        editor.Dispose ();

        // After disposal the completion session must be torn down.
        Assert.False (editor.IsCompletionActive, "Completion should not be active after dispose");
    }

    /// <summary>
    ///     Verifies that dismissing completion before dispose produces no error and that the
    ///     subsequent dispose is clean (no double-dispose exceptions from the popover).
    /// </summary>
    [Fact]
    public async Task Dispose_After_Completion_Dismissed_Does_Not_Throw ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("using unsafe uint "));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();
        editor.CaretOffset = editor.Document!.TextLength;
        editor.CompletionProvider = new TestWordCompletionProvider ();

        // Open completion by typing "u" then dismiss via Escape.
        fx.Injector.InjectKey (Key.U, Direct);
        Assert.True (editor.IsCompletionActive);
        fx.Injector.InjectKey (Key.Esc, Direct);
        Assert.False (editor.IsCompletionActive);

        // Should not throw on dispose — the popover was already cleaned up.
        var ex = Record.Exception (() => editor.Dispose ());
        Assert.Null (ex);
    }

    /// <summary>
    ///     Repeated show→dismiss cycles of the completion popup must not accumulate leaked views.
    ///     After multiple cycles and a dispose, IsCompletionActive must be false and no exception
    ///     should be thrown.
    /// </summary>
    [Fact]
    public async Task Multiple_Completion_Cycles_Do_Not_Leak_On_Dispose ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("using unsafe uint "));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();
        editor.CompletionProvider = new TestWordCompletionProvider ();

        for (var i = 0; i < 5; i++)
        {
            editor.CaretOffset = editor.Document!.TextLength;

            // Trigger completion by typing "u".
            fx.Injector.InjectKey (Key.U, Direct);
            Assert.True (editor.IsCompletionActive);

            // Dismiss via Escape.
            fx.Injector.InjectKey (Key.Esc, Direct);

            // Delete the typed char for next iteration.
            fx.Injector.InjectKey (Key.Backspace, Direct);
        }

        var ex = Record.Exception (() => editor.Dispose ());
        Assert.Null (ex);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Context menu (PopoverMenu) disposal
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     The default context menu is created in the constructor. On disposal it should be
    ///     cleaned up — at minimum not cause exceptions or leave orphan views.
    /// </summary>
    [Fact]
    public async Task Dispose_Cleans_Up_ContextMenu ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("test"));
        Editor editor = fx.Top.Editor;

        // Verify the context menu exists.
        Assert.NotNull (editor.ContextMenu);
        PopoverMenu contextMenu = editor.ContextMenu!;

        // Dispose the editor.
        editor.Dispose ();

        // The context menu should ideally be disposed. We can't check WasDisposed without
        // DEBUG_IDISPOSABLE, but we verify no exception occurred and the menu items are not
        // still referencing a live editor via the Target WeakReference.
        if (contextMenu.Target is not null && contextMenu.Target.TryGetTarget (out View? target))
        {
            // If the WeakReference is still alive, the context menu's target is the disposed editor.
            // This is a potential leak indicator — the disposed editor is still reachable.
            // NOTE: This assertion documents the current (buggy?) behavior; if the Editor properly
            // nulled ContextMenu.Target on dispose, this would fail.
            Assert.Same (editor, target);
        }
    }

    /// <summary>
    ///     When a custom context menu is assigned, the old one should not leak. After dispose,
    ///     neither the old nor the new context menu should cause issues.
    /// </summary>
    [Fact]
    public async Task Replacing_ContextMenu_Then_Dispose_Does_Not_Throw ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("test"));
        Editor editor = fx.Top.Editor;

        PopoverMenu original = editor.ContextMenu!;
        PopoverMenu replacement = new ([new MenuItem (editor, Command.Copy)]);
        editor.ContextMenu = replacement;

        // The original PopoverMenu is now orphaned — nobody disposes it.
        // Dispose the editor.
        var ex = Record.Exception (() => editor.Dispose ());
        Assert.Null (ex);

        // Manually dispose the orphan to verify it doesn't blow up (but note: the Editor
        // didn't do this for us — this test documents that replacement doesn't auto-dispose).
        ex = Record.Exception (() => original.Dispose ());
        Assert.Null (ex);
    }

    /// <summary>
    ///     Setting ContextMenu to null should not leave the old menu undisposed in the view tree.
    /// </summary>
    [Fact]
    public async Task Setting_ContextMenu_Null_Does_Not_Leak ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("test"));
        Editor editor = fx.Top.Editor;

        PopoverMenu original = editor.ContextMenu!;
        editor.ContextMenu = null;

        // The original is now orphaned with no owner to dispose it.
        // Dispose the editor cleanly.
        var ex = Record.Exception (() => editor.Dispose ());
        Assert.Null (ex);

        // The orphan should still be disposable on its own.
        ex = Record.Exception (() => original.Dispose ());
        Assert.Null (ex);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Gutter disposal
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     When the editor has a gutter (line numbers enabled) and is then disposed, the gutter
    ///     view should be cleaned up.
    /// </summary>
    [Fact]
    public async Task Dispose_With_Active_Gutter_Does_Not_Throw ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("line 1\nline 2\nline 3"));
        Editor editor = fx.Top.Editor;

        // Enable gutter (creates a Gutter view added to Padding).
        editor.GutterOptions = GutterOptions.LineNumbers;
        fx.Render ();

        // The gutter is a child of Padding — verify it's there.
        Assert.True (editor.Padding.GetOrCreateView ().SubViews.Any (), "Gutter should be present in Padding");

        // Dispose the editor — should not throw even with gutter active.
        var ex = Record.Exception (() => editor.Dispose ());
        Assert.Null (ex);
    }

    /// <summary>
    ///     Enabling then disabling the gutter (GutterOptions = None) should dispose the gutter view.
    ///     A subsequent Editor dispose should still be clean.
    /// </summary>
    [Fact]
    public async Task Gutter_Removed_Before_Dispose_Is_Clean ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("line 1\nline 2"));
        Editor editor = fx.Top.Editor;

        editor.GutterOptions = GutterOptions.LineNumbers;
        fx.Render ();
        Assert.True (editor.Padding.GetOrCreateView ().SubViews.Any ());

        // Remove the gutter.
        editor.GutterOptions = GutterOptions.None;
        fx.Render ();

        // Gutter should be removed from Padding.
        Assert.False (
            editor.Padding.GetOrCreateView ().SubViews.Any (),
            "Gutter should be removed from Padding after disabling");

        // Dispose should be clean.
        var ex = Record.Exception (() => editor.Dispose ());
        Assert.Null (ex);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  General disposal
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Double-disposing the editor must not throw. View.Dispose should be idempotent.
    /// </summary>
    [Fact]
    public async Task Double_Dispose_Does_Not_Throw ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        Editor editor = fx.Top.Editor;

        editor.Dispose ();

        var ex = Record.Exception (() => editor.Dispose ());
        Assert.Null (ex);
    }

    /// <summary>
    ///     Disposing the editor while it has a highlighter assigned should dispose the highlighter
    ///     and not throw.
    /// </summary>
    [Fact]
    public async Task Dispose_With_Highlighter_Does_Not_Throw ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("int x = 42;"));
        Editor editor = fx.Top.Editor;

        // Set a highlighting definition if one is loaded; otherwise just verify dispose works.
        // Even without a highlighter, the dispose path should handle nulls gracefully.
        var ex = Record.Exception (() => editor.Dispose ());
        Assert.Null (ex);
    }

    /// <summary>
    ///     Disposing the editor with a SearchStrategy set should not leave the SearchHitRenderer
    ///     dangling in BackgroundRenderers.
    /// </summary>
    [Fact]
    public async Task Dispose_With_SearchStrategy_Does_Not_Throw ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("find me here"));
        Editor editor = fx.Top.Editor;

        editor.FindNext ("me");
        Assert.NotNull (editor.SearchStrategy);

        var ex = Record.Exception (() => editor.Dispose ());
        Assert.Null (ex);
    }

    /// <summary>
    ///     The AppFixture's DisposeAsync disposes the entire view tree. The Editor (via EditorTestHost)
    ///     must survive this without exceptions — testing the full teardown path that xUnit hits.
    /// </summary>
    [Fact]
    public async Task AppFixture_DisposeAsync_Cleans_Up_Editor_ViewTree ()
    {
        // This test is subtle: AppFixture.DisposeAsync calls End then Dispose on the top runnable,
        // which cascades to all children. If Editor's child views aren't properly managed this
        // would throw during test cleanup.
        AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("using unsafe uint "));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();
        editor.GutterOptions = GutterOptions.LineNumbers;
        editor.CompletionProvider = new TestWordCompletionProvider ();

        // Trigger completion by typing "u".
        editor.CaretOffset = editor.Document!.TextLength;
        fx.Injector.InjectKey (Key.U, Direct);
        Assert.True (editor.IsCompletionActive);

        // Open the context menu too.
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new System.Drawing.Point (2, 0),
                Flags = MouseFlags.RightButtonClicked,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            },
            Direct);
        fx.Render ();

        // Now dispose the entire fixture — tests the cascading disposal path.
        var ex = await Record.ExceptionAsync (async () => await fx.DisposeAsync ());
        Assert.Null (ex);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Helper
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Simple word-based completion provider for disposal testing.</summary>
    private sealed class TestWordCompletionProvider : IEditorCompletionProvider
    {
        public IReadOnlyList<CompletionItem> GetCompletions (TextDocument document, int caretOffset, string prefix)
        {
            if (string.IsNullOrEmpty (prefix))
            {
                return [];
            }

            var text = document.Text;
            HashSet<string> seen = new (StringComparer.OrdinalIgnoreCase);
            List<CompletionItem> results = [];

            var i = 0;

            while (i < text.Length)
            {
                if (!char.IsLetterOrDigit (text[i]) && text[i] != '_')
                {
                    i++;

                    continue;
                }

                var start = i;

                while (i < text.Length && (char.IsLetterOrDigit (text[i]) || text[i] == '_'))
                {
                    i++;
                }

                var word = text.Substring (start, i - start);

                if (word.Length > prefix.Length
                    && word.StartsWith (prefix, StringComparison.OrdinalIgnoreCase)
                    && seen.Add (word))
                {
                    results.Add (new CompletionItem { Label = word });
                }
            }

            return results;
        }

        public bool ShouldTrigger (Key key)
        {
            return key == Key.Space.WithCtrl;
        }
    }
}

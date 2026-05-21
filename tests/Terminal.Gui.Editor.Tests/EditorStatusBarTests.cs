// Copilot - claude-opus-4.6

using Terminal.Gui.Document;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

public class EditorStatusBarTests
{
    [Fact]
    public void Constructor_SingleEditor_SetsActiveEditor ()
    {
        Editor editor = new ();
        EditorStatusBar statusBar = new (editor);

        Assert.Same (editor, statusBar.ActiveEditor);
    }

    [Fact]
    public void Constructor_FuncProvider_SetsActiveEditor ()
    {
        Editor editor = new ();
        EditorStatusBar statusBar = new (() => editor);

        Assert.Same (editor, statusBar.ActiveEditor);
    }

    [Fact]
    public void LocShortcut_InitializesToLn1Col1 ()
    {
        Editor editor = new ();
        EditorStatusBar statusBar = new (editor);

        Assert.Contains ("Ln 1", statusBar.LocShortcut.Title);
        Assert.Contains ("Col 1", statusBar.LocShortcut.Title);
    }

    [Fact]
    public void OverwriteShortcut_DefaultsToINS ()
    {
        Editor editor = new ();
        EditorStatusBar statusBar = new (editor);

        Assert.Equal ("INS", statusBar.OverwriteShortcut.Title);
    }

    [Fact]
    public void UpdateLocShortcut_ReflectsCaretPosition ()
    {
        Editor editor = new ();
        editor.Document = new TextDocument ("Hello\nWorld");
        editor.CaretOffset = 7; // 'o' in "World" → line 2, col 2.
        EditorStatusBar statusBar = new (editor);

        statusBar.UpdateLocShortcut ();

        Assert.Contains ("Ln 2", statusBar.LocShortcut.Title);
        Assert.Contains ("Col 2", statusBar.LocShortcut.Title);
    }

    [Fact]
    public void UpdateOverwriteShortcut_ReflectsEditorMode ()
    {
        Editor editor = new ();
        editor.OverwriteMode = true;
        EditorStatusBar statusBar = new (editor);

        statusBar.UpdateOverwriteShortcut ();

        Assert.Equal ("OVR", statusBar.OverwriteShortcut.Title);
    }

    [Fact]
    public void LanguageShortcut_DefaultsToPlainText ()
    {
        Editor editor = new ();
        EditorStatusBar statusBar = new (editor);

        Assert.Equal ("Plain Text", statusBar.LanguageShortcut.Title);
    }

    [Fact]
    public void ExtraShortcuts_Appends ()
    {
        Editor editor = new ();
        EditorStatusBar statusBar = new (editor);

        statusBar.ExtraShortcuts.Add (new Shortcut { Title = "Custom" });
        statusBar.RebuildShortcuts ();

        Assert.Single (statusBar.ExtraShortcuts);
    }

    [Fact]
    public void CaretChanged_UpdatesLocShortcut ()
    {
        Editor editor = new ();
        editor.Document = new TextDocument ("Hello\nWorld");
        EditorStatusBar statusBar = new (editor);

        // Move caret to trigger event.
        editor.CaretOffset = 7;

        Assert.Contains ("Ln 2", statusBar.LocShortcut.Title);
    }

    [Fact]
    public void OverwriteModeChanged_UpdatesOverwriteShortcut ()
    {
        Editor editor = new ();
        EditorStatusBar statusBar = new (editor);

        editor.OverwriteMode = true;

        Assert.Equal ("OVR", statusBar.OverwriteShortcut.Title);
    }

    [Fact]
    public void SwitchEditor_ReSubscribesEvents ()
    {
        Editor editor1 = new ();
        Editor editor2 = new ();
        editor2.Document = new TextDocument ("Second");
        editor2.CaretOffset = 3;

        Editor current = editor1;
        EditorStatusBar statusBar = new (() => current);

        current = editor2;
        statusBar.SwitchEditor ();

        Assert.Contains ("Col 4", statusBar.LocShortcut.Title);
    }
}

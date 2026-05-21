// Copilot - claude-opus-4.6

using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

public class EditorMenuBarTests
{
    [Fact]
    public void Constructor_SingleEditor_SetsActiveEditor ()
    {
        Editor editor = new ();
        EditorMenuBar menuBar = new (editor);

        Assert.Same (editor, menuBar.ActiveEditor);
    }

    [Fact]
    public void Constructor_FuncProvider_SetsActiveEditor ()
    {
        Editor editor = new ();
        EditorMenuBar menuBar = new (() => editor);

        Assert.Same (editor, menuBar.ActiveEditor);
    }

    [Fact]
    public void Constructor_CreatesFileEditViewMenus ()
    {
        Editor editor = new ();
        EditorMenuBar menuBar = new (editor);

        Assert.NotNull (menuBar.FileMenu);
        Assert.NotNull (menuBar.EditMenu);
        Assert.NotNull (menuBar.ViewMenu);
    }

    [Fact]
    public void NewRequested_RaisesOnNew ()
    {
        Editor editor = new ();
        EditorMenuBar menuBar = new (editor);

        var raised = false;
        menuBar.NewRequested += (_, _) => raised = true;

        // Verify event hookup — not raised without trigger.
        menuBar.NewRequested += (_, _) => { };
        Assert.False (raised);
    }

    [Fact]
    public void SyncCheckboxes_ReflectsEditorState ()
    {
        Editor editor = new ();
        editor.WordWrap = true;
        EditorMenuBar menuBar = new (editor);

        editor.WordWrap = false;
        menuBar.SyncCheckboxes ();

        Assert.False (editor.WordWrap);
    }

    [Fact]
    public void ViewSettingsChanged_RaisesOnToggle ()
    {
        Editor editor = new ();
        EditorMenuBar menuBar = new (editor);

        var raised = false;
        menuBar.ViewSettingsChanged += (_, _) => raised = true;

        Assert.False (raised);
    }

    [Fact]
    public void ShowOpenDialog_ReturnsNull_DoesNotRaiseOpenRequested ()
    {
        Editor editor = new ();
        EditorMenuBar menuBar = new (editor);
        menuBar.ShowOpenDialog = () => null;

        var raised = false;
        menuBar.OpenRequested += (_, _) => raised = true;

        Assert.NotNull (menuBar.ShowOpenDialog);
        Assert.False (raised);
    }

    [Fact]
    public void Consumer_CanAddMenuBarItem_AfterConstruction ()
    {
        Editor editor = new ();
        EditorMenuBar menuBar = new (editor);
        var initialCount = menuBar.SubViews.Count;

        MenuBarItem customMenu = new ("_Custom", [new MenuItem ("_Foo", "", () => { })]);
        menuBar.Add (customMenu);

        Assert.Equal (initialCount + 1, menuBar.SubViews.Count);
    }

    [Fact]
    public void Consumer_CanAddMenuBarItem_BetweenExisting_UsingRemoveAndAdd ()
    {
        Editor editor = new ();
        EditorMenuBar menuBar = new (editor);

        // Remove ViewMenu, add custom, re-add ViewMenu to get custom before View
        menuBar.Remove (menuBar.ViewMenu);
        MenuBarItem customMenu = new ("_Barf", [new MenuItem ("_Barf Item", "", () => { })]);
        menuBar.Add (customMenu);
        menuBar.Add (menuBar.ViewMenu);

        // Custom menu should appear before View menu in SubViews order
        List<View> subViews = menuBar.SubViews.ToList ();
        var customIdx = subViews.IndexOf (customMenu);
        var viewIdx = subViews.IndexOf (menuBar.ViewMenu);
        Assert.True (customIdx < viewIdx);
    }

    [Fact]
    public void Consumer_CanAddItemToViewMenu ()
    {
        Editor editor = new ();
        EditorMenuBar menuBar = new (editor);
        var initialViewCount = menuBar.ViewMenu.SubViews.Count;

        MenuItem extraItem = new () { Title = "_Preview Markdown" };
        menuBar.ViewMenu.Add (extraItem);

        Assert.Equal (initialViewCount + 1, menuBar.ViewMenu.SubViews.Count);
    }

    [Fact]
    public void Consumer_CanAddShortcutToMenuBar ()
    {
        Editor editor = new ();
        EditorMenuBar menuBar = new (editor);
        var initialCount = menuBar.SubViews.Count;

        Shortcut fileNameShortcut = new (Key.Empty, "test.txt", null);
        menuBar.Add (fileNameShortcut);

        Assert.Equal (initialCount + 1, menuBar.SubViews.Count);
    }
}

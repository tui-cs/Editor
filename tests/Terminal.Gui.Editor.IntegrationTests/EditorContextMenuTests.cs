// CoPilot - gpt-4.1

using System.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Integration tests for the built-in <see cref="Editor.ContextMenu" /> — right-click / Command.Context
///     triggers the default editing context menu, items reflect state, and the menu is replaceable / suppressible.
/// </summary>
public class EditorContextMenuTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task RightClick_Shows_Default_ContextMenu ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        fx.Top.Editor.SetFocus ();

        DriverAssert.ContentsDoesNotContain (fx.Driver, "Undo");

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (4, 0),
                Flags = MouseFlags.RightButtonClicked,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            },
            Direct);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Undo");
        DriverAssert.ContentsContains (fx.Driver, "Redo");
        DriverAssert.ContentsContains (fx.Driver, "Cut");
        DriverAssert.ContentsContains (fx.Driver, "Copy");
        DriverAssert.ContentsContains (fx.Driver, "Paste");
        DriverAssert.ContentsContains (fx.Driver, "Select all");
    }

    [Fact]
    public async Task ContextMenu_Null_Suppresses_Menu ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ContextMenu = null;

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (4, 0),
                Flags = MouseFlags.RightButtonClicked,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            },
            Direct);
        fx.Render ();

        DriverAssert.ContentsDoesNotContain (fx.Driver, "Undo");
        DriverAssert.ContentsDoesNotContain (fx.Driver, "Select all");
    }

    [Fact]
    public async Task ReadOnly_Disables_Mutating_Items ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ReadOnly = true;

        // Select text so Copy is enabled
        fx.Top.Editor.CaretOffset = 0;
        fx.Top.Editor.SelectAll ();

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (4, 0),
                Flags = MouseFlags.RightButtonClicked,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            },
            Direct);

        // Verify state: Copy should be enabled (has selection), Cut/Paste/Undo/Redo should be disabled
        PopoverMenu? menu = fx.Top.Editor.ContextMenu;
        Assert.NotNull (menu);

        foreach (View child in menu.Root!.SubViews)
        {
            if (child is not MenuItem menuItem)
            {
                continue;
            }

            switch (menuItem.Command)
            {
                case Command.Cut:
                case Command.Paste:
                case Command.Undo:
                case Command.Redo:
                    Assert.False (menuItem.Enabled,
                        $"{menuItem.Command} should be disabled when ReadOnly");

                    break;
                case Command.Copy:
                    Assert.True (menuItem.Enabled, "Copy should be enabled when there is a selection");

                    break;
                case Command.SelectAll:
                    Assert.True (menuItem.Enabled, "Select All should always be enabled");

                    break;
            }
        }
    }

    [Fact]
    public async Task No_Selection_Disables_Cut_And_Copy ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        // No selection — right-click to open menu
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (4, 0),
                Flags = MouseFlags.RightButtonClicked,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            },
            Direct);

        PopoverMenu? menu = fx.Top.Editor.ContextMenu;
        Assert.NotNull (menu);

        foreach (View child in menu.Root!.SubViews)
        {
            if (child is not MenuItem menuItem)
            {
                continue;
            }

            switch (menuItem.Command)
            {
                case Command.Cut:
                case Command.Copy:
                    Assert.False (menuItem.Enabled,
                        $"{menuItem.Command} should be disabled when there is no selection");

                    break;
            }
        }
    }

    [Fact]
    public async Task Undo_Enabled_After_Edit ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.SetFocus ();

        // Make an edit so undo becomes available
        fx.Top.Editor.Document!.Insert (5, "X");

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (2, 0),
                Flags = MouseFlags.RightButtonClicked,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            },
            Direct);

        PopoverMenu? menu = fx.Top.Editor.ContextMenu;
        Assert.NotNull (menu);

        foreach (View child in menu.Root!.SubViews)
        {
            if (child is MenuItem { Command: Command.Undo } undoItem)
            {
                Assert.True (undoItem.Enabled, "Undo should be enabled after an edit");
            }
        }
    }

    [Fact]
    public async Task Default_ContextMenu_Is_Not_Null ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("test"));

        Assert.NotNull (fx.Top.Editor.ContextMenu);
    }
}

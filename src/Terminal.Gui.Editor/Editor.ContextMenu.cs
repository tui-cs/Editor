using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    private PopoverMenu? _contextMenu;

    /// <summary>
    ///     Gets or sets the built-in context menu shown on right-click or <see cref="Command.Context" />.
    ///     Defaults to a standard editing menu (Undo, Redo, Cut, Copy, Paste, Select All) whose items
    ///     are state-aware — mutating items are disabled when <see cref="ReadOnly" /> is <see langword="true" />,
    ///     Copy / Cut are disabled when there is no selection, and Undo / Redo reflect the undo stack.
    ///     Set to <see langword="null" /> to suppress the context menu entirely; set to a custom
    ///     <see cref="PopoverMenu" /> to replace it.
    /// </summary>
    public PopoverMenu? ContextMenu
    {
        get => _contextMenu;
        set
        {
            _contextMenu = value;

            if (_contextMenu is not null)
            {
                _contextMenu.Target = new WeakReference<View> (this);
            }
        }
    }

    /// <summary>Creates the default editing context menu items.</summary>
    private View[] CreateDefaultContextMenuItems ()
    {
        return
        [
            new MenuItem { Title = "_Undo", Command = Command.Undo, Action = () => InvokeCommand (Command.Undo) },
            new MenuItem { Title = "_Redo", Command = Command.Redo, Action = () => InvokeCommand (Command.Redo) },
            new Line (),
            new MenuItem { Title = "Cu_t", Command = Command.Cut, Action = () => InvokeCommand (Command.Cut) },
            new MenuItem { Title = "_Copy", Command = Command.Copy, Action = () => InvokeCommand (Command.Copy) },
            new MenuItem { Title = "_Paste", Command = Command.Paste, Action = () => InvokeCommand (Command.Paste) },
            new Line (),
            new MenuItem
            {
                Title = "Select _all", Command = Command.SelectAll, Action = () => InvokeCommand (Command.SelectAll)
            }
        ];
    }

    /// <summary>
    ///     Updates the <see cref="Enabled" /> state of the context menu items to reflect the current
    ///     editor state (ReadOnly, selection, clipboard, undo/redo).
    /// </summary>
    private void UpdateContextMenuState ()
    {
        if (_contextMenu?.Root is null)
        {
            return;
        }

        var hasSelection = HasSelection;
        var canUndo = !ReadOnly && _document is { UndoStack.CanUndo: true };
        var canRedo = !ReadOnly && _document is { UndoStack.CanRedo: true };
        var canPaste = !ReadOnly;
        var canCut = !ReadOnly && hasSelection;

        foreach (View child in _contextMenu.Root.SubViews)
        {
            if (child is not MenuItem menuItem)
            {
                continue;
            }

            menuItem.Enabled = menuItem.Command switch
            {
                Command.Undo => canUndo,
                Command.Redo => canRedo,
                Command.Cut => canCut,
                Command.Copy => hasSelection,
                Command.Paste => canPaste,
                _ => true
            };
        }
    }

    /// <summary>
    ///     Shows the context menu at the given screen position, after updating item state.
    /// </summary>
    private void ShowContextMenu (Point? screenPosition = null)
    {
        if (_contextMenu is null)
        {
            return;
        }

        UpdateContextMenuState ();
        _contextMenu.MakeVisible (screenPosition);
    }

    /// <summary>Builds and assigns the default context menu.</summary>
    private void InitializeDefaultContextMenu ()
    {
        ContextMenu = new PopoverMenu (CreateDefaultContextMenuItems ());
    }
}

using Terminal.Gui.App;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    // ─── Editor-specific Command IDs ────────────────────────────────────────────
    // Terminal.Gui's Command enum (int-backed) ends at 77 as of v2.1.0.
    // We claim a block starting at 1000 so upstream additions never collide.

    /// <summary>Command that inserts a tab or indents the current selection.</summary>
    public static readonly Command InsertTabCommand = (Command)1000;

    /// <summary>Command that removes one indentation level from the current line or selection.</summary>
    public static readonly Command UnindentCommand = (Command)1001;

    /// <summary>Command that moves to the next search match.</summary>
    public static readonly Command FindNextCommand = (Command)1002;

    /// <summary>Command that moves to the previous search match.</summary>
    public static readonly Command FindPreviousCommand = (Command)1003;

    /// <summary>Command that raises the <see cref="FindRequested" /> event (open Find UI).</summary>
    public static readonly Command FindCommand = (Command)1004;

    /// <summary>Command that raises the <see cref="ReplaceRequested" /> event (open Replace UI).</summary>
    public static readonly Command ReplaceCommand = (Command)1005;

    /// <summary>
    ///     Editor-specific default key bindings layered on top of <see cref="View.DefaultKeyBindings" />.
    ///     The base layer already maps cursor / Home / End / PageUp / PageDown (and their Shift variants)
    ///     to the corresponding movement and *Extend <see cref="Command" />s, plus Ctrl+A → SelectAll;
    ///     this dictionary covers what's editor-specific (Enter, Backspace/Delete, Ctrl+Z / Ctrl+Y, the
    ///     Ctrl+Home/End whole-document binds, Tab/Shift+Tab indentation, and Find/Replace).
    /// </summary>
    /// <remarks>
    ///     Process-wide static. Do not mutate from parallel tests — see Terminal.Gui's same convention
    ///     on <see cref="Terminal.Gui.Views.TextField.DefaultKeyBindings" />.
    /// </remarks>
    public new static Dictionary<Command, PlatformKeyBinding>? DefaultKeyBindings { get; set; } = new ()
    {
        [Command.Start] = Bind.All (Key.Home.WithCtrl),
        [Command.End] = Bind.All (Key.End.WithCtrl),
        [Command.NewLine] = Bind.All (Key.Enter),
        [Command.DeleteCharLeft] = Bind.All (Key.Backspace),
        [Command.DeleteCharRight] = Bind.All (Key.Delete),
        [Command.Undo] = Bind.All (Key.Z.WithCtrl),
        [Command.Redo] = Bind.All (Key.Y.WithCtrl, Key.Z.WithCtrl.WithShift),
        [Command.Cut] = Bind.All (Key.X.WithCtrl),
        [Command.Copy] = Bind.All (Key.C.WithCtrl),
        [Command.Paste] = Bind.All (Key.V.WithCtrl),
        [Command.Collapse] = Bind.All (Key.M.WithCtrl),
        [InsertTabCommand] = Bind.All (Key.Tab),
        [UnindentCommand] = Bind.All (Key.Tab.WithShift),
        [FindNextCommand] = Bind.All (Key.F3),
        [FindPreviousCommand] = Bind.All (Key.F3.WithShift),
        [FindCommand] = Bind.All (Key.F.WithCtrl),
        [ReplaceCommand] = Bind.All (Key.H.WithCtrl)
    };

    private void CreateCommandsAndBindings ()
    {
        // View's SetupKeyboard pre-binds Enter→Accept and Space→Activate. In a text editor those
        // are the literal characters, so reclaim them before applying layered bindings.
        KeyBindings.Remove (Key.Enter);
        KeyBindings.Remove (Key.Space);

        // Plain movement (collapses any existing selection)
        AddCommand (Command.Left, () => MoveCaretByCollapsing (-1));
        AddCommand (Command.Right, () => MoveCaretByCollapsing (1));
        AddCommand (Command.Up, () => MoveCaretVerticallyCollapsing (-1));
        AddCommand (Command.Down, () => MoveCaretVerticallyCollapsing (1));
        AddCommand (Command.LeftStart, MoveCaretToLineStart);
        AddCommand (Command.RightEnd, MoveCaretToLineEnd);
        AddCommand (Command.Start, () => SetCaretAndReturnTrue (0));
        AddCommand (Command.End, () => SetCaretAndReturnTrue (_document!.TextLength));
        AddCommand (Command.PageUp, () => MoveCaretVerticallyCollapsing (-Math.Max (1, Viewport.Height)));
        AddCommand (Command.PageDown, () => MoveCaretVerticallyCollapsing (Math.Max (1, Viewport.Height)));
        AddCommand (Command.ScrollUp, () => ScrollVerticalCommand (-1));
        AddCommand (Command.ScrollDown, () => ScrollVerticalCommand (1));
        AddCommand (Command.ScrollLeft, () => ScrollHorizontalCommand (-1));
        AddCommand (Command.ScrollRight, () => ScrollHorizontalCommand (1));

        // Selection-extending movement
        AddCommand (Command.LeftExtend, () => ExtendCommand (() => ExtendCaretBy (-1)));
        AddCommand (Command.RightExtend, () => ExtendCommand (() => ExtendCaretBy (1)));
        AddCommand (Command.UpExtend, () => ExtendCommand (() => ExtendCaretVertically (-1)));
        AddCommand (Command.DownExtend, () => ExtendCommand (() => ExtendCaretVertically (1)));
        AddCommand (Command.LeftStartExtend,
            () => ExtendCommand (() => ExtendCaretTo (_document!.GetLineByOffset (CaretOffset).Offset)));

        AddCommand (Command.RightEndExtend, () => ExtendCommand (() =>
        {
            DocumentLine line = _document!.GetLineByOffset (CaretOffset);
            ExtendCaretTo (line.Offset + line.Length);
        }));

        AddCommand (Command.StartExtend, () => ExtendCommand (() => ExtendCaretTo (0)));
        AddCommand (Command.EndExtend, () => ExtendCommand (() => ExtendCaretTo (_document!.TextLength)));
        AddCommand (Command.PageUpExtend,
            () => ExtendCommand (() => ExtendCaretVertically (-Math.Max (1, Viewport.Height))));
        AddCommand (Command.PageDownExtend,
            () => ExtendCommand (() => ExtendCaretVertically (Math.Max (1, Viewport.Height))));

        // Selection ops
        AddCommand (Command.SelectAll, () =>
        {
            SelectAll ();

            return true;
        });

        // Clipboard
        AddCommand (Command.Copy, () =>
        {
            if (!HasSelection)
            {
                return true;
            }

            App?.Clipboard?.TrySetClipboardData (SelectedText);

            return true;
        });

        AddCommand (Command.Cut, () =>
        {
            if (ReadOnly || !HasSelection)
            {
                return true;
            }

            // Abort cut if clipboard write fails — never destroy text without placing it on the clipboard.
            if (App?.Clipboard?.TrySetClipboardData (SelectedText) is not true)
            {
                return true;
            }

            using (_document!.RunUpdate ())
            {
                ReplaceSelection (string.Empty);
            }

            return true;
        });

        AddCommand (Command.Paste, () =>
        {
            if (ReadOnly)
            {
                return true;
            }

            IClipboard? clipboard = App?.Clipboard;

            if (clipboard is null || !clipboard.TryGetClipboardData (out var contents))
            {
                return true;
            }

            using (_document!.RunUpdate ())
            {
                if (HasSelection)
                {
                    ReplaceSelection (contents);
                }
                else
                {
                    _document.Insert (CaretOffset, contents);
                }
            }

            return true;
        });

        // Editing — selection-aware
        AddCommand (Command.NewLine, InsertNewLineWithAutoIndent);
        AddCommand (Command.DeleteCharLeft, DeleteLeft);
        AddCommand (Command.DeleteCharRight, DeleteRight);

        // History
        AddCommand (Command.Undo, () =>
        {
            if (ReadOnly || !_document!.UndoStack.CanUndo)
            {
                return true;
            }

            ClearSelection ();
            _document!.UndoStack.Undo ();

            return true;
        });

        AddCommand (Command.Redo, () =>
        {
            if (ReadOnly || !_document!.UndoStack.CanRedo)
            {
                return true;
            }

            ClearSelection ();
            _document!.UndoStack.Redo ();

            return true;
        });

        // Folding
        AddCommand (Command.Collapse, ToggleFoldUnderCaret);

        // Indentation
        AddCommand (InsertTabCommand, () => InsertTab ());
        AddCommand (UnindentCommand, () => Unindent ());

        // Find / Replace
        AddCommand (FindNextCommand, () =>
        {
            FindNext ();

            return true;
        });

        AddCommand (FindPreviousCommand, () =>
        {
            FindPrevious ();

            return true;
        });

        AddCommand (FindCommand, () =>
        {
            FindRequested?.Invoke (this, EventArgs.Empty);

            return true;
        });

        AddCommand (ReplaceCommand, () =>
        {
            ReplaceRequested?.Invoke (this, EventArgs.Empty);

            return true;
        });

        ApplyKeyBindings (View.DefaultKeyBindings, DefaultKeyBindings);

        MouseBindings.Add (MouseFlags.WheeledUp, Command.ScrollUp);
        MouseBindings.Add (MouseFlags.WheeledDown, Command.ScrollDown);
        MouseBindings.Add (MouseFlags.WheeledLeft, Command.ScrollLeft);
        MouseBindings.Add (MouseFlags.WheeledRight, Command.ScrollRight);

        // Allow scroll commands from gutter subviews (hosted in Padding) to bubble up to this Editor.
        CommandsToBubbleUp = [Command.ScrollUp, Command.ScrollDown, Command.ScrollLeft, Command.ScrollRight];
    }

    private bool? ExtendCommand (Action extend)
    {
        extend ();

        return true;
    }

    private bool? MoveCaretByCollapsing (int delta)
    {
        MoveCaretByCollapsingSelection (delta);

        return true;
    }

    private bool? MoveCaretVerticallyCollapsing (int delta)
    {
        MoveCaretVerticallyCollapsingSelection (delta);

        return true;
    }

    private bool? ScrollVerticalCommand (int delta)
    {
        if (_document is null || ScrollVertical (delta) != true)
        {
            return false;
        }

        SetNeedsDraw ();

        return true;
    }

    private bool? ScrollHorizontalCommand (int delta)
    {
        if (_document is null || ScrollHorizontal (delta) != true)
        {
            return false;
        }

        SetNeedsDraw ();

        return true;
    }

    private bool? InsertNewLineWithAutoIndent ()
    {
        if (ReadOnly)
        {
            return true;
        }

        // Wrap both the newline insertion and the auto-indent in a single undo group
        // so that one Ctrl+Z undoes the entire Enter operation.
        using (_document!.RunUpdate ())
        {
            if (HasSelection)
            {
                ReplaceSelection ("\n");
            }
            else
            {
                _document.Insert (CaretOffset, "\n");
            }

            // After the newline is inserted the caret sits at the start of the new line.
            // Ask the indentation strategy to fill in leading whitespace.
            if (IndentationStrategy is { } strategy)
            {
                DocumentLine newLine = _document.GetLineByOffset (CaretOffset);
                strategy.IndentLine (_document, newLine);
            }
        }

        return true;
    }

    private bool? InsertOrReplace (string text)
    {
        if (ReadOnly)
        {
            return true;
        }

        if (HasSelection)
        {
            ReplaceSelection (text);
        }
        else
        {
            _document!.Insert (CaretOffset, text);
        }

        return true;
    }

    private bool? DeleteLeft ()
    {
        if (ReadOnly)
        {
            return true;
        }

        if (HasSelection)
        {
            ReplaceSelection (string.Empty);
        }
        else if (TryDeleteIndentationLeft ())
        {
            return true;
        }
        else if (CaretOffset > 0)
        {
            _document!.Remove (CaretOffset - 1, 1);
        }

        return true;
    }

    private bool? DeleteRight ()
    {
        if (ReadOnly)
        {
            return true;
        }

        if (HasSelection)
        {
            ReplaceSelection (string.Empty);
        }
        else if (CaretOffset < _document!.TextLength)
        {
            _document!.Remove (CaretOffset, 1);
        }

        return true;
    }

    private bool? SetCaretAndReturnTrue (int offset)
    {
        CaretOffset = offset;

        return true;
    }

    private bool? MoveCaretToLineStart ()
    {
        DocumentLine line = _document!.GetLineByOffset (CaretOffset);
        CaretOffset = line.Offset;

        return true;
    }

    private bool? MoveCaretToLineEnd ()
    {
        DocumentLine line = _document!.GetLineByOffset (CaretOffset);
        CaretOffset = line.Offset + line.Length;

        return true;
    }

    private bool? ToggleFoldUnderCaret ()
    {
        if (FoldingManager is not { } fm || _document is null)
        {
            return true;
        }

        var caretOffset = CaretOffset;
        DocumentLine caretLine = _document.GetLineByOffset (caretOffset);

        // First, try to find a fold starting on this line.
        FoldingSection? fold = fm.GetFoldingAtLine (caretLine.LineNumber);

        // If none, try folds containing the caret.
        if (fold is null)
        {
            foreach (FoldingSection fs in fm.GetFoldingsContaining (caretOffset))
            {
                fold = fs;

                break;
            }
        }

        if (fold is not null)
        {
            fold.IsFolded = !fold.IsFolded;
        }

        return true;
    }
}

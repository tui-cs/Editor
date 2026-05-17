using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    /// <summary>
    ///     Editor-specific default key bindings layered on top of <see cref="View.DefaultKeyBindings" />.
    ///     The base layer already maps cursor / Home / End / PageUp / PageDown (and their Shift variants)
    ///     to the corresponding movement and *Extend <see cref="Command" />s, plus Ctrl+A → SelectAll;
    ///     this dictionary covers what's editor-specific (Enter, Backspace/Delete, Ctrl+Z / Ctrl+Y, the
    ///     Ctrl+Home/End whole-document binds).
    /// </summary>
    /// <remarks>
    ///     Process-wide static. Do not mutate from parallel tests — see Terminal.Gui's same convention
    ///     on <see cref="Terminal.Gui.Views.TextField.DefaultKeyBindings" />.
    /// </remarks>
    [ConfigurationProperty (Scope = typeof (SettingsScope))]
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
        [Command.InsertTab] = Bind.All (Key.Tab),
        [Command.Unindent] = Bind.All (Key.Tab.WithShift),
        [Command.FindNext] = Bind.All (Key.F3),
        [Command.FindPrevious] = Bind.All (Key.F3.WithShift),
        [Command.Find] = Bind.All (Key.F.WithCtrl),
        [Command.Replace] = Bind.All (Key.H.WithCtrl),

        // Vertical multi-caret — VS Code parity (Ctrl+Alt+Up/Down). A PlatformKeyBinding, so a
        // user whose terminal/WM grabs the chord overrides it via View.ViewKeyBindings config;
        // no editor-specific fallback chord. macOS uses the same chord pending real-terminal
        // validation (specs/decisions.md DEC-006).
        [Command.InsertCaretAbove] = Bind.All (Key.CursorUp.WithCtrl.WithAlt),
        [Command.InsertCaretBelow] = Bind.All (Key.CursorDown.WithCtrl.WithAlt),
        [Command.WordLeft] = Bind.All (Key.CursorLeft.WithCtrl),
        [Command.WordRight] = Bind.All (Key.CursorRight.WithCtrl),
        [Command.WordLeftExtend] = Bind.All (Key.CursorLeft.WithCtrl.WithShift),
        [Command.WordRightExtend] = Bind.All (Key.CursorRight.WithCtrl.WithShift),
        [Command.KillWordLeft] = Bind.All (Key.Backspace.WithCtrl),
        [Command.KillWordRight] = Bind.All (Key.Delete.WithCtrl),
        [Command.ToggleOverwrite] = Bind.All (Key.InsertChar)
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

        // Editing — selection-aware (multi-caret aware)
        AddCommand (Command.NewLine, MultiCaretNewLine);
        AddCommand (Command.DeleteCharLeft, MultiCaretDeleteLeft);
        AddCommand (Command.DeleteCharRight, MultiCaretDeleteRight);

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

        // Indentation — InsertTab / Unindent return bool, wrapped for CommandImplementation (bool?).
        AddCommand (Command.InsertTab, () => InsertTab ());
        AddCommand (Command.Unindent, () => Unindent ());

        // Find / Replace
        AddCommand (Command.Find, InvokeFindRequested);
        AddCommand (Command.Replace, InvokeReplaceRequested);
        AddCommand (Command.FindNext, FindNextCommand);
        AddCommand (Command.FindPrevious, FindPreviousCommand);

        // Vertical multi-caret: add a caret one line above / below the block at the sticky column.
        AddCommand (Command.InsertCaretAbove, () => AddCaretVertically (-1));
        AddCommand (Command.InsertCaretBelow, () => AddCaretVertically (1));
        // Word navigation and kill
        AddCommand (Command.WordLeft, () =>
        {
            MoveCaretToWordBoundary (false);
            return true;
        });
        AddCommand (Command.WordRight, () =>
        {
            MoveCaretToWordBoundary (true);
            return true;
        });
        AddCommand (Command.WordLeftExtend,
            () => ExtendCommand (() => ExtendCaretTo (GetWordBoundaryOffset (CaretOffset, false))));
        AddCommand (Command.WordRightExtend,
            () => ExtendCommand (() => ExtendCaretTo (GetWordBoundaryOffset (CaretOffset, true))));
        AddCommand (Command.KillWordLeft, () =>
        {
            KillToWordBoundary (false);
            return true;
        });
        AddCommand (Command.KillWordRight, () =>
        {
            KillToWordBoundary (true);
            return true;
        });

        // Overwrite mode
        AddCommand (Command.ToggleOverwrite, () =>
        {
            OverwriteMode = !OverwriteMode;

            return true;
        });
        AddCommand (Command.EnableOverwrite, () =>
        {
            OverwriteMode = true;

            return true;
        });
        AddCommand (Command.DisableOverwrite, () =>
        {
            OverwriteMode = false;

            return true;
        });

        // Context menu — return false when suppressed so the command can bubble.
        AddCommand (Command.Context, () =>
        {
            if (ContextMenu is null)
            {
                return false;
            }

            ShowContextMenu ();

            return true;
        });

        ApplyKeyBindings (View.DefaultKeyBindings, DefaultKeyBindings);

        // Reclaim Tab / Shift+Tab from the framework's default focus-cycling bindings so our
        // InsertTab / Unindent commands fire instead.
        KeyBindings.Remove (Key.Tab);
        KeyBindings.Remove (Key.Tab.WithShift);
        KeyBindings.Add (Key.Tab, Command.InsertTab);
        KeyBindings.Add (Key.Tab.WithShift, Command.Unindent);

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
        else if (OverwriteMode && _document is not null)
        {
            OverwriteAtCaret (text);
        }
        else
        {
            _document!.Insert (CaretOffset, text);
        }

        return true;
    }

    /// <summary>
    ///     Overwrites the grapheme at the caret with <paramref name="text" />. If the caret is at
    ///     line-end, falls back to a plain insert so the newline is not consumed.
    /// </summary>
    private void OverwriteAtCaret (string text)
    {
        OverwriteAtOffset (CaretOffset, text);
    }

    /// <summary>
    ///     Overwrites the grapheme at the given <paramref name="offset" /> with <paramref name="text" />.
    ///     If the offset is at line-end, falls back to a plain insert so the newline is not consumed.
    /// </summary>
    private void OverwriteAtOffset (int offset, string text)
    {
        DocumentLine line = _document!.GetLineByOffset (offset);
        var lineEnd = line.Offset + line.Length;

        if (offset >= lineEnd)
        {
            // At or past end-of-line content — just insert.
            _document.Insert (offset, text);

            return;
        }

        // Determine the length of the grapheme cluster under the caret so wide runes are
        // replaced atomically. StringInfo.GetNextTextElementLength gives cluster length in chars.
        var remaining = _document.GetText (offset, lineEnd - offset);
        var graphemeLength = System.Globalization.StringInfo.GetNextTextElementLength (remaining);

        // Use RemoveAndInsert so that the caret anchor (AfterInsertion) moves past the
        // inserted text. The default same-length Replace uses CharacterReplace mode which
        // does not move anchors at all.
        _document.Replace (offset, graphemeLength, text, OffsetChangeMappingType.RemoveAndInsert);
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

    private bool? InvokeFindRequested ()
    {
        FindRequested?.Invoke (this, EventArgs.Empty);

        return true;
    }

    private bool? InvokeReplaceRequested ()
    {
        ReplaceRequested?.Invoke (this, EventArgs.Empty);

        return true;
    }

    private bool? FindNextCommand ()
    {
        FindNext ();

        return true;
    }

    private bool? FindPreviousCommand ()
    {
        FindPrevious ();

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

        fold?.IsFolded = !fold.IsFolded;

        return true;
    }
}

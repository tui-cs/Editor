using System.Text;
using Terminal.Gui.Input;
using Terminal.Gui.Text.Document;

namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <inheritdoc />
    protected override bool OnKeyDown (Key key)
    {
        if (HandleNavigation (key))
        {
            return true;
        }

        if (HandleHistory (key))
        {
            return true;
        }

        if (HandleEditing (key))
        {
            return true;
        }

        return false;
    }

    private bool HandleNavigation (Key key)
    {
        if (key == Key.CursorLeft)
        {
            CaretOffset = _caretOffset - 1;

            return true;
        }

        if (key == Key.CursorRight)
        {
            CaretOffset = _caretOffset + 1;

            return true;
        }

        if (key == Key.CursorUp)
        {
            MoveCaretVertically (-1);

            return true;
        }

        if (key == Key.CursorDown)
        {
            MoveCaretVertically (1);

            return true;
        }

        if (key == Key.Home.WithCtrl)
        {
            CaretOffset = 0;

            return true;
        }

        if (key == Key.End.WithCtrl)
        {
            CaretOffset = _document?.TextLength ?? 0;

            return true;
        }

        if (key == Key.Home)
        {
            DocumentLine? line = _document?.GetLineByOffset (_caretOffset);
            CaretOffset = line?.Offset ?? 0;

            return true;
        }

        if (key == Key.End)
        {
            DocumentLine? line = _document?.GetLineByOffset (_caretOffset);
            CaretOffset = (line?.Offset ?? 0) + (line?.Length ?? 0);

            return true;
        }

        if (key == Key.PageUp)
        {
            MoveCaretVertically (-Math.Max (1, Viewport.Height));

            return true;
        }

        if (key == Key.PageDown)
        {
            MoveCaretVertically (Math.Max (1, Viewport.Height));

            return true;
        }

        return false;
    }

    private void MoveCaretVertically (int delta)
    {
        if (_document is null)
        {
            return;
        }

        var targetLine = Math.Clamp (GetCaretLineIndex () + delta, 0, _document.LineCount - 1);
        DocumentLine line = _document.GetLineByNumber (targetLine + 1);
        var targetCol = Math.Min (_virtualCaretColumn, line.Length);

        // Preserve the sticky column across vertical moves — SetCaretOffset would otherwise reset it.
        var sticky = _virtualCaretColumn;
        SetCaretOffset (line.Offset + targetCol, false);
        _virtualCaretColumn = sticky;
    }

    private bool HandleHistory (Key key)
    {
        if (_document is null)
        {
            return false;
        }

        if (key == Key.Z.WithCtrl)
        {
            if (_document.UndoStack.CanUndo)
            {
                _document.UndoStack.Undo ();
            }

            return true;
        }

        if (key == Key.Y.WithCtrl || key == Key.Z.WithCtrl.WithShift)
        {
            if (_document.UndoStack.CanRedo)
            {
                _document.UndoStack.Redo ();
            }

            return true;
        }

        return false;
    }

    private bool HandleEditing (Key key)
    {
        if (_document is null)
        {
            return false;
        }

        if (key == Key.Backspace)
        {
            if (_caretOffset > 0)
            {
                _document.Remove (_caretOffset - 1, 1);
            }

            return true;
        }

        if (key == Key.Delete)
        {
            if (_caretOffset < _document.TextLength)
            {
                _document.Remove (_caretOffset, 1);
            }

            return true;
        }

        if (key == Key.Enter)
        {
            _document.Insert (_caretOffset, "\n");

            return true;
        }

        if (key.IsCtrl || key.IsAlt)
        {
            return false;
        }

        switch (key.AsRune)
        {
            case { } rune when rune != default && !Rune.IsControl (rune):
                _document.Insert (_caretOffset, rune.ToString ());

                return true;
            default:
                return false;
        }
    }
}

using System.Text;
using Terminal.Gui.Input;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    /// <summary>
    ///     Catches keystrokes that didn't match any registered <see cref="Command" /> binding (set up in
    ///     <see cref="CreateCommandsAndBindings" />) and inserts the typed rune into the document. Skips
    ///     modified keys and control characters — those are either bound elsewhere or not editor input.
    /// </summary>
    /// <inheritdoc />
    protected override bool OnKeyDownNotHandled (Key key)
    {
        if (TryHandleKeyboardColumnSelect (key))
        {
            return true;
        }

        if (key == Key.Esc && HasMultipleCarets)
        {
            ClearAdditionalCarets ();

            return true;
        }

        if (key.IsCtrl || key.IsAlt)
        {
            return false;
        }

        // Rune.IsControl already covers U+0000 (default(Rune)), so no explicit NUL guard is needed.
        if (key is not { AsRune: { } rune } || Rune.IsControl (rune))
        {
            return false;
        }

        if (ReadOnly)
        {
            return true;
        }

        if (HasMultipleCarets)
        {
            MultiCaretInsert (rune.ToString ());

            return true;
        }

        if (HasSelection)
        {
            ReplaceSelection (rune.ToString ());
        }
        else if (OverwriteMode && _document is not null)
        {
            OverwriteAtCaret (rune.ToString ());
        }
        else
        {
            _document!.Insert (CaretOffset, rune.ToString ());
        }

        return true;
    }

    private bool TryHandleKeyboardColumnSelect (Key key)
    {
        if (!key.IsCtrl || !key.IsShift || !key.IsAlt)
        {
            return false;
        }

        Key baseKey = key.NoCtrl.NoShift.NoAlt;

        if (baseKey == Key.CursorUp)
        {
            ColumnSelectByKeyboard (-1, 0);

            return true;
        }

        if (baseKey == Key.CursorDown)
        {
            ColumnSelectByKeyboard (1, 0);

            return true;
        }

        if (baseKey == Key.CursorLeft)
        {
            ColumnSelectByKeyboard (0, -1);

            return true;
        }

        if (baseKey == Key.CursorRight)
        {
            ColumnSelectByKeyboard (0, 1);

            return true;
        }

        var pageDelta = Math.Max (1, Viewport.Height);

        if (baseKey == Key.PageUp)
        {
            ColumnSelectByKeyboard (-pageDelta, 0);

            return true;
        }

        if (baseKey == Key.PageDown)
        {
            ColumnSelectByKeyboard (pageDelta, 0);

            return true;
        }

        return false;
    }
}

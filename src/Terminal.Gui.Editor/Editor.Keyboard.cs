using System.Text;
using Terminal.Gui.Input;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    /// <summary>
    ///     Intercepts every keystroke before dispatch so the kill-ring consecutive-kill flag is
    ///     correctly tracked. Snapshots <c>_lastCommandWasKill</c> into <c>_previousCommandWasKill</c>
    ///     (so the kill commands can read whether the preceding command was a kill for append/prepend
    ///     decisions), then clears <c>_lastCommandWasKill</c>. The kill commands
    ///     (<see cref="Command.CutToEndOfLine" /> / <see cref="Command.CutToStartOfLine" />) re-set
    ///     <c>_lastCommandWasKill</c> after executing; every other command leaves it cleared, which
    ///     breaks the "consecutive kill → append" run.
    /// </summary>
    /// <inheritdoc />
    protected override bool OnKeyDown (Key key)
    {
        _previousCommandWasKill = _lastCommandWasKill;
        _lastCommandWasKill = false;

        bool result = base.OnKeyDown (key);

        // Clear the snapshot so it does not leak into a subsequent InvokeCommand call.
        // If the dispatched command was a kill, _lastCommandWasKill is already true;
        // _previousCommandWasKill is no longer needed.
        _previousCommandWasKill = false;

        return result;
    }

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

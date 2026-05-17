using System.Text;
using Terminal.Gui.Input;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    /// <summary>
    ///     Runs before command bindings. When completion is active, intercepts navigation and
    ///     accept/dismiss keys (Enter, Tab, arrows, Esc) so they don't trigger the normal
    ///     editor command bindings. Also checks provider-specific trigger keys (e.g. Ctrl+Space).
    ///     Additionally tracks the kill-ring consecutive-kill flag: snapshots
    ///     <c>_lastCommandWasKill</c> into <c>_previousCommandWasKill</c>, then clears
    ///     <c>_lastCommandWasKill</c>. The kill commands re-set it after executing.
    /// </summary>
    /// <inheritdoc />
    protected override bool OnKeyDown (Key key)
    {
        if (HandleCompletionKey (key))
        {
            return true;
        }

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

        // After inserting a character, notify the completion system so it can open / filter.
        NotifyCompletionAfterInsert ();

        return true;
    }
}

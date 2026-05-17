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
        // Completion popup gets first priority for navigation / accept / dismiss / trigger keys.
        if (HandleCompletionKey (key))
        {
            return true;
        }

        if (key == Key.Esc && HasMultipleCarets)
        {
            ClearAdditionalCarets ();

            return true;
        }

        // Esc dismisses an active completion (handled above); when no popup is active, let it
        // fall through to multi-caret clear or default handling.
        if (key == Key.Esc && IsCompletionActive)
        {
            DismissCompletion ();

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
        else
        {
            _document!.Insert (CaretOffset, rune.ToString ());
        }

        // After inserting a character, notify the completion system so it can open / filter.
        NotifyCompletionAfterInsert ();

        return true;
    }
}

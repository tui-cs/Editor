using System.Text;
using Terminal.Gui.Input;
using Terminal.Gui.Text.Document;

namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <inheritdoc />
    protected override bool OnKeyDown (Key key)
    {
        if (_document is null)
        {
            return base.OnKeyDown (key);
        }

        // Tab / Shift+Tab: indent / unindent. Intercepted here because Terminal.Gui's Command
        // enum does not include Tab/BackTab, and the default Tab binding moves focus.
        if (key == Key.Tab)
        {
            HandleTab ();

            return true;
        }

        if (key == Key.Tab.WithShift)
        {
            HandleBackTab ();

            return true;
        }

        return base.OnKeyDown (key);
    }

    /// <summary>
    ///     Catches keystrokes that didn't match any registered <see cref="Command" /> binding (set up in
    ///     <see cref="CreateCommandsAndBindings" />) and inserts the typed rune into the document. Skips
    ///     modified keys and control characters — those are either bound elsewhere or not editor input.
    /// </summary>
    /// <inheritdoc />
    protected override bool OnKeyDownNotHandled (Key key)
    {
        if (key.IsCtrl || key.IsAlt)
        {
            return false;
        }

        // Rune.IsControl already covers U+0000 (default(Rune)), so no explicit NUL guard is needed.
        if (key is not { AsRune: { } rune } || Rune.IsControl (rune))
        {
            return false;
        }

        if (HasSelection)
        {
            ReplaceSelection (rune.ToString ());
        }
        else
        {
            _document!.Insert (_caretOffset, rune.ToString ());
        }

        return true;
    }
}

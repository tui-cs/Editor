using System.Text;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <summary>
    ///     Catches keystrokes that didn't match any registered <see cref="Command"/> binding (set up in
    ///     <see cref="CreateCommandsAndBindings"/>) and inserts the typed rune into the document. Skips
    ///     modified keys and control characters — those are either bound elsewhere or not editor input.
    /// </summary>
    /// <inheritdoc />
    protected override bool OnKeyDownNotHandled (Key key)
    {
        if (key == Key.Tab.WithShift)
        {
            Unindent ();

            return true;
        }

        if (key == Key.Tab)
        {
            InsertTab ();

            return true;
        }

        if (key.IsCtrl || key.IsAlt)
        {
            return false;
        }

        // Rune.IsControl already covers U+0000 (default(Rune)), so no explicit NUL guard is needed.
        if (key.AsRune is { } rune && !Rune.IsControl (rune))
        {
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

        return false;
    }
}

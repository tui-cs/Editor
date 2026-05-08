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
        if (key.IsCtrl || key.IsAlt)
        {
            return false;
        }

        if (key.AsRune is { } rune && rune != default && !Rune.IsControl (rune))
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

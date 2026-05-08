using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

/// <summary>
///     POC Editor control. 
/// </summary>
public class Editor : View
{
    /// <summary>Initializes a new <see cref="Editor"/>.</summary>
    public Editor ()
    {
        CanFocus = true;
        base.Text = "Hello world";
        Height = Dim.Auto ();
        Width = Dim.Auto ();
    }
}

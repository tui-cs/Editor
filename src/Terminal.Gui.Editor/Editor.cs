using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

/// <summary>
///     Single-line text editor View. Pre-alpha stub — for now, just renders <see cref="Text"/>
///     at viewport (0,0). Will grow into the multi-line, rope-backed editor described in
///     <c>specs/00-plan.md</c> phases 2–5.
/// </summary>
public class Editor : View
{
    /// <summary>Initializes a new <see cref="Editor"/>.</summary>
    public Editor ()
    {
        CanFocus = true;
    }

    /// <summary>Gets or sets the text rendered by the editor.</summary>
    public new string Text { get; set; } = "Hello world";

    /// <inheritdoc />
    protected override bool OnDrawingContent (DrawContext? context)
    {
        AddStr (0, 0, Text);

        return true;
    }
}

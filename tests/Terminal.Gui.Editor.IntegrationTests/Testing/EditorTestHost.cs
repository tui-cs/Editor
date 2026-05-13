// Claude - claude-opus-4-7

using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Editor;

namespace Terminal.Gui.Editor.IntegrationTests.Testing;

/// <summary>
///     Minimal <see cref="Window" /> hosting a single focused <see cref="Editor" /> that fills the whole
///     viewport. Lets <see cref="AppFixture{TRunnable}" /> drive editor input/render tests without
///     dragging in the rest of <c>ted</c>'s menu / status bar chrome.
/// </summary>
public sealed class EditorTestHost : Window
{
    /// <summary>Constructs a host with the given initial document text.</summary>
    public EditorTestHost (string initialText = "")
    {
        BorderStyle = LineStyle.None;
        Editor = new ()
        {
            Document = new (initialText),
            X = 0,
            Y = 0,
            Width = Dim.Fill (),
            Height = Dim.Fill ()
        };
        Add (Editor);
    }

    /// <summary>The editor under test.</summary>
    public Editor Editor { get; }
}

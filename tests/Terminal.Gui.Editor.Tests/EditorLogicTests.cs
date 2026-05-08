// Claude - claude-opus-4-7

using Terminal.Gui.Text.Document;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for <see cref="Editor" /> behaviors that don't need <c>Application.Init</c> — caret math,
///     document rewiring, edit-tracking arithmetic. UI-side tests live in IntegrationTests.
/// </summary>
public class EditorLogicTests
{
    [Fact]
    public void Default_DocumentSays_HelloWorld ()
    {
        Views.Editor editor = new();

        Assert.NotNull (editor.Document);
        Assert.Equal ("Hello world", editor.Document.Text);
    }

    [Fact]
    public void CaretOffset_Clamps_To_DocumentLength ()
    {
        Views.Editor editor = new() { Document = new TextDocument ("abc") };

        editor.CaretOffset = 99;
        Assert.Equal (3, editor.CaretOffset);

        editor.CaretOffset = -10;
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void CaretOffset_Set_Raises_CaretChanged ()
    {
        Views.Editor editor = new() { Document = new TextDocument ("abcdef") };
        var fires = 0;
        editor.CaretChanged += (_, _) => fires++;

        editor.CaretOffset = 3;
        Assert.Equal (1, fires);

        editor.CaretOffset = 3;
        Assert.Equal (1, fires);
    }

    [Fact]
    public void Document_Setter_Rewires_ChangeHandler ()
    {
        Views.Editor editor = new();
        TextDocument? original = editor.Document;

        TextDocument replacement = new("first\nsecond");
        editor.Document = replacement;

        Assert.Same (replacement, editor.Document);

        // Mutating the original after the swap should NOT raise DocumentChanged on the editor.
        var fired = false;
        editor.DocumentChanged += (_, _) => fired = true;
        original?.Insert (0, "x");
        Assert.False (fired);

        // Mutating the replacement does raise it.
        replacement.Insert (0, "y");
        Assert.True (fired);
    }

    [Fact]
    public void Caret_Tracks_Insertion_Before_It ()
    {
        Views.Editor editor = new() { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 5;

        editor.Document.Insert (0, ">>>");

        Assert.Equal (8, editor.CaretOffset);
    }

    [Fact]
    public void Caret_Stays_Put_For_Insertion_After_It ()
    {
        Views.Editor editor = new() { Document = new TextDocument ("hello") };
        editor.CaretOffset = 2;

        editor.Document.Insert (4, "X");

        Assert.Equal (2, editor.CaretOffset);
    }

    [Fact]
    public void Caret_Snaps_To_Removal_Start_When_Inside_Removed_Range ()
    {
        Views.Editor editor = new() { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 4;

        editor.Document.Remove (2, 5); // removes "llo w" → "heorld"

        Assert.Equal (2, editor.CaretOffset);
    }
}

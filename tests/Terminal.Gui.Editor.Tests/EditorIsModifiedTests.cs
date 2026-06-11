// Copilot - claude-opus-4.6

using Terminal.Gui.Editor.Document;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for <see cref="Editor.IsModified" /> and <see cref="Editor.ModifiedChanged" />.
/// </summary>
public class EditorIsModifiedTests
{
    [Fact]
    public void IsModified_FalseByDefault ()
    {
        Editor editor = new ();

        Assert.False (editor.IsModified);
    }

    [Fact]
    public void IsModified_TrueAfterEdit ()
    {
        Editor editor = new () { Document = new TextDocument ("hello") };

        Assert.False (editor.IsModified);

        editor.Document.Insert (5, " world");

        Assert.True (editor.IsModified);
    }

    [Fact]
    public void IsModified_FalseAfterUndoToClean ()
    {
        Editor editor = new () { Document = new TextDocument ("hello") };

        editor.Document.Insert (5, " world");
        Assert.True (editor.IsModified);

        editor.Document.UndoStack.Undo ();
        Assert.False (editor.IsModified);
    }

    [Fact]
    public void IsModified_FalseAfterMarkAsOriginalFile ()
    {
        Editor editor = new () { Document = new TextDocument ("hello") };

        editor.Document.Insert (5, " world");
        Assert.True (editor.IsModified);

        editor.Document.UndoStack.MarkAsOriginalFile ();
        Assert.False (editor.IsModified);
    }

    [Fact]
    public void ModifiedChanged_FiresOnDirtyTransition ()
    {
        Editor editor = new () { Document = new TextDocument ("abc") };
        var fires = 0;
        editor.ModifiedChanged += (_, _) => fires++;

        // Clean → dirty
        editor.Document.Insert (0, "x");
        Assert.Equal (1, fires);

        // Still dirty (no transition)
        editor.Document.Insert (1, "y");
        Assert.Equal (1, fires);

        // Dirty → clean
        editor.Document.UndoStack.Undo ();
        editor.Document.UndoStack.Undo ();
        Assert.Equal (2, fires);
    }

    [Fact]
    public void ModifiedChanged_FiresOnDocumentSwap ()
    {
        Editor editor = new () { Document = new TextDocument ("abc") };
        editor.Document.Insert (0, "x");
        Assert.True (editor.IsModified);

        var fires = 0;
        editor.ModifiedChanged += (_, _) => fires++;

        // Swap to a fresh (clean) document — IsModified transitions from true to false,
        // so ModifiedChanged must fire on the swap itself.
        editor.Document = new TextDocument ("new");
        Assert.False (editor.IsModified);
        Assert.Equal (1, fires);

        // Edit new doc should fire again
        editor.Document.Insert (0, "z");
        Assert.Equal (2, fires);
    }
}

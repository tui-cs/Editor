// Copilot - claude-opus-4.6

using Terminal.Gui.Document;
using Terminal.Gui.Input;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests that caret navigation and deletion operate on whole grapheme clusters,
///     not individual UTF-16 code units. Covers surrogate pairs (🦮 = 2 chars) and
///     ZWJ sequences (👨‍👩‍👧 = 11 chars).
/// </summary>
public class EditorGraphemeNavigationTests
{
    // 🦮 = U+1F9AE (surrogate pair, 2 UTF-16 code units, 2 columns)
    private const string GuideDog = "\U0001F9AE";

    // 👨‍👩‍👧 = U+1F468 U+200D U+1F469 U+200D U+1F467 (ZWJ sequence, 11 UTF-16 code units, 2 columns)
    private const string Family = "\U0001F468\u200D\U0001F469\u200D\U0001F467";

    [Fact]
    public void CursorRight_Skips_Entire_SurrogatePair ()
    {
        // "🦮X" — caret at 0, CursorRight should land at offset 2 (past the 2-char surrogate pair)
        Editor editor = new () { Document = new TextDocument (GuideDog + "X") };
        editor.CaretOffset = 0;

        editor.InvokeCommand (Command.Right);

        Assert.Equal (GuideDog.Length, editor.CaretOffset);
    }

    [Fact]
    public void CursorRight_Skips_Entire_ZWJ_Sequence ()
    {
        // "👨‍👩‍👧X" — caret at 0, CursorRight should land at offset 11 (past the ZWJ sequence)
        Editor editor = new () { Document = new TextDocument (Family + "X") };
        editor.CaretOffset = 0;

        editor.InvokeCommand (Command.Right);

        Assert.Equal (Family.Length, editor.CaretOffset);
    }

    [Fact]
    public void CursorLeft_Skips_Entire_SurrogatePair ()
    {
        // "🦮X" — caret at end of 🦮 (offset 2), CursorLeft should land at 0
        Editor editor = new () { Document = new TextDocument (GuideDog + "X") };
        editor.CaretOffset = GuideDog.Length;

        editor.InvokeCommand (Command.Left);

        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void CursorLeft_Skips_Entire_ZWJ_Sequence ()
    {
        // "👨‍👩‍👧X" — caret at end of 👨‍👩‍👧 (offset 11), CursorLeft should land at 0
        Editor editor = new () { Document = new TextDocument (Family + "X") };
        editor.CaretOffset = Family.Length;

        editor.InvokeCommand (Command.Left);

        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void Backspace_Deletes_Entire_SurrogatePair ()
    {
        // "🦮X" — caret after 🦮, backspace removes the whole emoji
        Editor editor = new () { Document = new TextDocument (GuideDog + "X") };
        editor.CaretOffset = GuideDog.Length;

        editor.InvokeCommand (Command.DeleteCharLeft);

        Assert.Equal ("X", editor.Document!.Text);
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void Backspace_Deletes_Entire_ZWJ_Sequence ()
    {
        // "👨‍👩‍👧X" — caret after ZWJ sequence, backspace removes the whole thing
        Editor editor = new () { Document = new TextDocument (Family + "X") };
        editor.CaretOffset = Family.Length;

        editor.InvokeCommand (Command.DeleteCharLeft);

        Assert.Equal ("X", editor.Document!.Text);
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void Delete_Forward_Removes_Entire_SurrogatePair ()
    {
        // "🦮X" — caret at 0, Delete removes the whole emoji
        Editor editor = new () { Document = new TextDocument (GuideDog + "X") };
        editor.CaretOffset = 0;

        editor.InvokeCommand (Command.DeleteCharRight);

        Assert.Equal ("X", editor.Document!.Text);
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void Delete_Forward_Removes_Entire_ZWJ_Sequence ()
    {
        // "👨‍👩‍👧X" — caret at 0, Delete removes the whole ZWJ sequence
        Editor editor = new () { Document = new TextDocument (Family + "X") };
        editor.CaretOffset = 0;

        editor.InvokeCommand (Command.DeleteCharRight);

        Assert.Equal ("X", editor.Document!.Text);
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void CursorRight_Then_Left_Roundtrips_Through_Graphemes ()
    {
        // "🦮👨‍👩‍👧" — moving right twice then left twice returns to start
        Editor editor = new () { Document = new TextDocument (GuideDog + Family) };
        editor.CaretOffset = 0;

        editor.InvokeCommand (Command.Right); // past 🦮
        Assert.Equal (GuideDog.Length, editor.CaretOffset);

        editor.InvokeCommand (Command.Right); // past 👨‍👩‍👧
        Assert.Equal (GuideDog.Length + Family.Length, editor.CaretOffset);

        editor.InvokeCommand (Command.Left); // back before 👨‍👩‍👧
        Assert.Equal (GuideDog.Length, editor.CaretOffset);

        editor.InvokeCommand (Command.Left); // back before 🦮
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void ShiftRight_Extends_Selection_By_Grapheme ()
    {
        // "🦮X" — Shift+Right from 0 should select the whole emoji
        Editor editor = new () { Document = new TextDocument (GuideDog + "X") };
        editor.CaretOffset = 0;

        editor.InvokeCommand (Command.RightExtend);

        Assert.Equal (GuideDog.Length, editor.CaretOffset);
        Assert.True (editor.HasSelection);
        Assert.Equal (0, editor.SelectionStart);
        Assert.Equal (GuideDog.Length, editor.SelectionEnd);
    }

    [Fact]
    public void ShiftLeft_Extends_Selection_By_Grapheme ()
    {
        // "X🦮" — caret at end of 🦮, Shift+Left should select the whole emoji
        Editor editor = new () { Document = new TextDocument ("X" + GuideDog) };
        editor.CaretOffset = 1 + GuideDog.Length;

        editor.InvokeCommand (Command.LeftExtend);

        Assert.Equal (1, editor.CaretOffset);
        Assert.True (editor.HasSelection);
        Assert.Equal (1, editor.SelectionStart);
        Assert.Equal (1 + GuideDog.Length, editor.SelectionEnd);
    }

    [Fact]
    public void Multiple_Backspaces_On_Line_With_Two_Graphemes_Clears_Line ()
    {
        // "🦮👨‍👩‍👧" — 2 backspaces from end clears the line
        Editor editor = new () { Document = new TextDocument (GuideDog + Family) };
        editor.CaretOffset = GuideDog.Length + Family.Length;

        editor.InvokeCommand (Command.DeleteCharLeft); // removes 👨‍👩‍👧
        Assert.Equal (GuideDog, editor.Document!.Text);

        editor.InvokeCommand (Command.DeleteCharLeft); // removes 🦮
        Assert.Equal (string.Empty, editor.Document!.Text);
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void CursorRight_At_End_Of_Line_Crosses_To_Next_Line ()
    {
        // Multiline: "🦮\nX" — caret at end of first line (offset 2), CursorRight crosses newline
        Editor editor = new () { Document = new TextDocument (GuideDog + "\nX"), Multiline = true };
        editor.CaretOffset = GuideDog.Length;

        editor.InvokeCommand (Command.Right);

        // Should land at start of second line (offset after the \n)
        Assert.Equal (GuideDog.Length + 1, editor.CaretOffset);
    }

    [Fact]
    public void CursorLeft_At_Start_Of_Line_Crosses_To_Previous_Line ()
    {
        // Multiline: "X\n🦮" — caret at start of second line, CursorLeft crosses newline
        Editor editor = new () { Document = new TextDocument ("X\n" + GuideDog), Multiline = true };
        editor.CaretOffset = 2; // start of second line

        editor.InvokeCommand (Command.Left);

        // Should land at end of first line text (offset 1 = after 'X')
        Assert.Equal (1, editor.CaretOffset);
    }
}

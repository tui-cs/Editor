using Terminal.Gui.Editor.Document;
using Terminal.Gui.Input;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

public class EditorNewlineNavigationTests
{
    [Fact]
    public void CRLF_Is_Treated_As_Single_Newline_For_Navigate_And_Delete ()
    {
        // Use Environment.NewLine so this test exercises CRLF on Windows.
        // The contract we want: one Left/Right/DeleteCharLeft/DeleteCharRight crosses/removes the whole newline,
        // not half (\r then \n or \n then \r).
        var newline = Environment.NewLine;
        var text = "a" + newline + "b";

        Editor editor = new () { Document = new TextDocument (text), Multiline = true };

        // ── Navigation across newline ─────────────────────────────────────
        editor.CaretOffset = 1; // after 'a'
        editor.InvokeCommand (Command.Right);
        Assert.Equal (1 + newline.Length, editor.CaretOffset);

        editor.InvokeCommand (Command.Left);
        Assert.Equal (1, editor.CaretOffset);

        // ── Backspace removes the whole newline ───────────────────────────
        editor.Document.Text = text;
        editor.CaretOffset = 1 + newline.Length; // start of 'b'

        editor.InvokeCommand (Command.DeleteCharLeft);

        Assert.Equal ("ab", editor.Document.Text);
        Assert.Equal (1, editor.CaretOffset);

        // ── Delete forward removes the whole newline ──────────────────────
        editor.Document.Text = text;
        editor.CaretOffset = 1; // after 'a'

        editor.InvokeCommand (Command.DeleteCharRight);

        Assert.Equal ("ab", editor.Document.Text);
        Assert.Equal (1, editor.CaretOffset);
    }
}

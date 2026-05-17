// CoPilot - claude-sonnet-4.6

using Terminal.Gui.Document;
using Terminal.Gui.Input;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for Ctrl+Left / Ctrl+Right word navigation, Ctrl+Shift+Left / Ctrl+Shift+Right
///     word selection extension, and Ctrl+Backspace / Ctrl+Delete word kill commands.
/// </summary>
public class EditorWordNavigationTests
{
    // ── WordLeft (Ctrl+Left) ─────────────────────────────────────────────

    [Fact]
    public void WordLeft_FromMiddleOfWord_MovesToWordStart ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 8; // inside "world"

        editor.InvokeCommand (Command.WordLeft);

        Assert.Equal (6, editor.CaretOffset); // start of "world"
    }

    [Fact]
    public void WordLeft_FromWordStart_MovesToStartOfPreviousWord ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 6; // start of "world"

        editor.InvokeCommand (Command.WordLeft);

        Assert.Equal (0, editor.CaretOffset); // start of "hello"
    }

    [Fact]
    public void WordLeft_AtDocumentStart_StaysAtZero ()
    {
        Editor editor = new () { Document = new TextDocument ("hello") };
        editor.CaretOffset = 0;

        editor.InvokeCommand (Command.WordLeft);

        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void WordLeft_CollapsesExistingSelection_ToSelectionStart ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.SelectRange (2, 5); // selects "llo w", caret at 7

        editor.InvokeCommand (Command.WordLeft);

        Assert.False (editor.HasSelection);
        // After collapsing to SelectionStart (2), WordLeft moves to previous word start (0)
        Assert.Equal (0, editor.CaretOffset);
    }

    // ── WordRight (Ctrl+Right) ───────────────────────────────────────────

    [Fact]
    public void WordRight_FromMiddleOfWord_MovesToNextWordStart ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 2; // inside "hello"

        editor.InvokeCommand (Command.WordRight);

        Assert.Equal (6, editor.CaretOffset); // start of "world"
    }

    [Fact]
    public void WordRight_AtDocumentEnd_StaysAtTextLength ()
    {
        Editor editor = new () { Document = new TextDocument ("hello") };
        editor.CaretOffset = editor.Document!.TextLength;

        editor.InvokeCommand (Command.WordRight);

        Assert.Equal (editor.Document.TextLength, editor.CaretOffset);
    }

    [Fact]
    public void WordRight_CollapsesExistingSelection_ToSelectionEnd ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.SelectRange (2, 4); // selects "lo w", caret at 6

        editor.InvokeCommand (Command.WordRight);

        Assert.False (editor.HasSelection);
        // After collapsing to SelectionEnd (6), WordRight moves to end of document (11)
        Assert.Equal (11, editor.CaretOffset);
    }

    // ── WordLeftExtend (Ctrl+Shift+Left) ────────────────────────────────

    [Fact]
    public void WordLeftExtend_CreatesSelection_ExpandingLeft ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 8; // inside "world"

        editor.InvokeCommand (Command.WordLeftExtend);

        Assert.True (editor.HasSelection);
        Assert.Equal (6, editor.SelectionStart); // start of "world"
        Assert.Equal (8, editor.SelectionEnd);
    }

    [Fact]
    public void WordLeftExtend_ExtendsExistingSelection ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 8;
        editor.InvokeCommand (Command.WordLeftExtend); // anchor at 8, caret at 6

        editor.InvokeCommand (Command.WordLeftExtend); // extend further left

        Assert.True (editor.HasSelection);
        Assert.Equal (0, editor.SelectionStart); // start of "hello"
        Assert.Equal (8, editor.SelectionEnd);
    }

    // ── WordRightExtend (Ctrl+Shift+Right) ──────────────────────────────

    [Fact]
    public void WordRightExtend_CreatesSelection_ExpandingRight ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 0;

        editor.InvokeCommand (Command.WordRightExtend);

        Assert.True (editor.HasSelection);
        Assert.Equal (0, editor.SelectionStart);
        Assert.True (editor.SelectionEnd > 0);
    }

    // ── KillWordLeft (Ctrl+Backspace) ────────────────────────────────────

    [Fact]
    public void KillWordLeft_DeletesFromWordStartToCaret ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 9; // inside "world": "wor" before caret (w=6,o=7,r=8,l=9)

        editor.InvokeCommand (Command.KillWordLeft);

        Assert.Equal ("hello ld", editor.Document!.Text);
        Assert.Equal (6, editor.CaretOffset);
    }

    [Fact]
    public void KillWordLeft_AtDocumentStart_IsNoOp ()
    {
        Editor editor = new () { Document = new TextDocument ("hello") };
        editor.CaretOffset = 0;

        editor.InvokeCommand (Command.KillWordLeft);

        Assert.Equal ("hello", editor.Document!.Text);
    }

    [Fact]
    public void KillWordLeft_ReadOnly_IsNoOp ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world"), ReadOnly = true };
        editor.CaretOffset = 8;

        editor.InvokeCommand (Command.KillWordLeft);

        Assert.Equal ("hello world", editor.Document!.Text);
    }

    [Fact]
    public void KillWordLeft_WithSelection_DeletesSelection ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.SelectRange (0, 5); // select "hello"

        editor.InvokeCommand (Command.KillWordLeft);

        Assert.Equal (" world", editor.Document!.Text);
        Assert.False (editor.HasSelection);
    }

    // ── KillWordRight (Ctrl+Delete) ──────────────────────────────────────

    [Fact]
    public void KillWordRight_DeletesFromCaretToNextWordStart ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 0; // at start, kill "hello"

        editor.InvokeCommand (Command.KillWordRight);

        Assert.False (editor.Document!.Text.StartsWith ("hello"));
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void KillWordRight_AtDocumentEnd_IsNoOp ()
    {
        Editor editor = new () { Document = new TextDocument ("hello") };
        editor.CaretOffset = editor.Document!.TextLength;

        editor.InvokeCommand (Command.KillWordRight);

        Assert.Equal ("hello", editor.Document.Text);
    }
}

# Indentation

## Indenting and un-indenting

> All keys shown in this guide are the defaults and can be changed. See [Customizing Keybindings and Themes](configuration.md).

| Action | Key |
|---|---|
| Indent current line | `Tab` |
| Un-indent current line | `Shift+Tab` |

When text spanning multiple lines is selected, `Tab` indents every line in the selection and `Shift+Tab` un-indents every line.

## Tab size

The host application controls the indentation size (number of spaces per indent level). In `ted` this is shown as a numeric control in the status bar (default 4).

## Spaces vs. tabs

The **Convert Tabs to Spaces** setting determines what is inserted when you press `Tab`:

- **On** (default in `ted`): the editor inserts the appropriate number of spaces to reach the next tab stop.
- **Off**: a literal tab character is inserted.

This setting does not retroactively convert existing tab characters in the document; it only affects new input.

## Showing tab characters

When **Show Tabs** is enabled, tab characters are rendered with a visible glyph (↹) in the first cell of the tab stop. This is useful for distinguishing mixed indentation.

## Auto-indent on Enter

When an **indentation strategy** is active, pressing `Enter` copies the leading whitespace of the current line onto the new line automatically. This keeps your indentation level consistent as you type.

Auto-indent and the newline are treated as a single undo step.

## Indentation-aware Backspace

When the caret is positioned in the leading whitespace of a line and no text is selected, pressing `Backspace` removes a full indentation level's worth of spaces rather than a single character, making it easy to step back one level at a time.

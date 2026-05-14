# Navigation

## Keyboard navigation

> All keys shown in this guide are the defaults and can be changed. See [Customizing Keybindings and Themes](configuration.md).

| Action | Key |
|---|---|
| Move left one character | `←` |
| Move right one character | `→` |
| Move up one line | `↑` |
| Move down one line | `↓` |
| Move to start of line | `Home` |
| Move to end of line | `End` |
| Move to start of document | `Ctrl+Home` |
| Move to end of document | `Ctrl+End` |
| Move up one page | `Page Up` |
| Move down one page | `Page Down` |

All navigation keys collapse any active selection; to extend the selection while moving see [Selection](selection.md).

## Sticky virtual column

When you move the caret vertically (up/down/page), the editor remembers the column you were in. If you pass through a shorter line, the caret snaps to the end of that line temporarily, but the next vertical move returns to your original column as soon as a longer line is reached.

For example, if the caret is at column 20, pressing `↓` onto a 5-character line moves the caret to column 5 — but pressing `↓` again onto a 25-character line snaps the caret back to column 20.

## Mouse navigation

Click anywhere in the text area to place the caret at that position.

## Scrolling

| Action | Key / gesture |
|---|---|
| Scroll up one line | `Mouse wheel up` |
| Scroll down one line | `Mouse wheel down` |
| Scroll left | `Mouse wheel left` (horizontal wheel or modifier+wheel) |
| Scroll right | `Mouse wheel right` |

Scrolling does not move the caret — it only shifts the visible region of the document. The scrollbars (if enabled by the host application) can also be used to scroll.

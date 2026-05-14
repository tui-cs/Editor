# Terminal.Gui Editor — Help

`Terminal.Gui.Editor` is a fully-featured multi-line text editor `View` for Terminal.Gui applications. This help covers everything you need to get the most out of the editor — whether you are using it inside a consumer app like `ted` or building your own application on top of the library.

## Contents

| Topic | Description |
|---|---|
| [Editing](editing.md) | Typing, inserting and deleting text, Enter |
| [Navigation](navigation.md) | Moving the caret with the keyboard and mouse |
| [Selection](selection.md) | Selecting text with keyboard and mouse |
| [Clipboard](clipboard.md) | Cut, copy, and paste |
| [Undo & Redo](undo-redo.md) | Undo and redo edit history |
| [Find & Replace](find-and-replace.md) | Searching and replacing text |
| [Folding](folding.md) | Collapsing and expanding code regions |
| [Indentation](indentation.md) | Tab and indentation behaviour |
| [Syntax Highlighting](syntax-highlighting.md) | Language-aware colour coding |
| [Word Wrap](word-wrap.md) | Soft-wrapping long lines |
| [Read-Only Mode](read-only.md) | Viewing text without editing |
| [Gutter](gutter.md) | Line numbers and fold indicators |
| [Keyboard Reference](keyboard-reference.md) | Complete list of default keyboard shortcuts |

## Quick orientation

The editor is laid out as a single editing surface. Depending on how the host application has configured it, you may also see:

- a **gutter** on the left showing line numbers and/or fold indicators,
- **scrollbars** on the right and bottom edges,
- a **status bar** at the bottom showing the current line and column position.

Keyboard shortcuts follow common conventions (arrow keys to move, `Ctrl+C`/`X`/`V` for clipboard, `Ctrl+Z`/`Y` for undo/redo). All shortcuts are remappable by the host application; the defaults are documented in [Keyboard Reference](keyboard-reference.md).

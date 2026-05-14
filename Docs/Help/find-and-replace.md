# Find & Replace

## Opening the Find bar

> All keys shown in this guide are the defaults and can be changed. See [Customizing Keybindings and Themes](configuration.md).

Press `Ctrl+F` to open the Find bar (or Find & Replace dialog, depending on how the host application has wired the editor).

## Opening the Replace bar

Press `Ctrl+H` to open the Replace dialog directly.

## Finding text

1. Open Find (`Ctrl+F`) and type your search term.
2. Press `Enter` or `F3` to jump to the next match.
3. Press `Shift+F3` to jump to the previous match.
4. The search wraps around when it reaches the end (or start) of the document.

## Search options

| Option | Description |
|---|---|
| **Match case** | When checked, the search is case-sensitive. |
| **Whole word** | When checked, only standalone word matches are found (e.g. searching "cat" does not match "catalog"). |
| **Regular expression** | When checked, the search term is interpreted as a .NET regular expression. |

## Replacing text

1. Open Replace (`Ctrl+H`) and enter the search term and the replacement text.
2. Click **Replace** (or press `Enter` in the Replace field) to replace the current match and advance to the next.
3. Click **Replace All** to replace every occurrence in the document in a single operation.

**Replace All** is a single undo step — pressing `Ctrl+Z` once undoes all replacements.

## Regular expression replacements

When **Regular expression** mode is enabled, the replacement text can use capture group back-references. For example:

| Search | Replace | Input | Output |
|---|---|---|---|
| `(\w+)=(\d+)` | `$2:$1` | `count=42` | `42:count` |

## Keyboard shortcuts

| Action | Key |
|---|---|
| Open Find | `Ctrl+F` |
| Open Replace | `Ctrl+H` |
| Find next | `F3` |
| Find previous | `Shift+F3` |

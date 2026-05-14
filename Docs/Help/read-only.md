# Read-Only Mode

Read-only mode lets you view a document without the risk of accidentally modifying it. It is useful for log viewers, read-only file browsing, diff displays, and any situation where the document should not change.

## What is disabled in read-only mode

The following operations are blocked when read-only mode is active:

- Typing (inserting characters)
- `Backspace` and `Delete`
- `Enter` (inserting newlines)
- `Tab` / `Shift+Tab` (indentation)
- `Ctrl+X` (cut)
- `Ctrl+V` (paste)
- `Ctrl+Z` (undo) and `Ctrl+Y` (redo)

## What still works in read-only mode

- All navigation keys (`←`, `→`, `↑`, `↓`, `Home`, `End`, `Page Up`, `Page Down`, `Ctrl+Home`, `Ctrl+End`)
- Selection (`Shift+` navigation keys, `Ctrl+A`, click-and-drag)
- `Ctrl+C` (copy the selection to the clipboard)
- Find / Find Next / Find Previous (`Ctrl+F`, `F3`, `Shift+F3`)
- Scrolling (mouse wheel, scrollbars)

## Enabling read-only mode

In `ted`, launch with the `--read-only` flag:

```sh
dotnet run --project examples/ted -- --read-only path/to/file.cs
```

Host applications can set `Editor.ReadOnly = true` programmatically at any time.

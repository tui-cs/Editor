# Clipboard

The editor integrates with the operating system clipboard via Terminal.Gui's clipboard abstraction, so text cut or copied from the editor can be pasted into any other application and vice versa.

## Copying text

1. Select the text you want to copy (see [Selection](selection.md)).
2. Press `Ctrl+C`.

The selected text is placed on the clipboard. The selection remains active and the document is not modified.

## Cutting text

1. Select the text you want to cut.
2. Press `Ctrl+X`.

The selected text is removed from the document and placed on the clipboard in a single undo step. If the clipboard write fails, the text is **not** removed — the editor never destroys text it cannot save to the clipboard.

## Pasting text

Press `Ctrl+V` to paste the clipboard contents at the current caret position.

- If text is selected, the paste replaces the selection.
- If no text is selected, the clipboard contents are inserted at the caret.

Paste is a single undo step regardless of how much text is inserted.

## Notes

- All clipboard operations are disabled when the editor is in [read-only mode](read-only.md). `Ctrl+C` (copy) works in read-only mode; `Ctrl+X` and `Ctrl+V` do not modify the document.
- The host application determines which clipboard backend is used (system, in-process, etc.). In most terminal environments `Ctrl+C`/`Ctrl+X`/`Ctrl+V` reach the OS clipboard.

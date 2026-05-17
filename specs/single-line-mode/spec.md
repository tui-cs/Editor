# Single-Line / Embeddable-Input Mode

**Status**: Implemented  
**Issue**: [#147](https://github.com/gui-cs/Editor/issues/147)  
**Decision**: [DEC-008](../decisions.md#dec-008-single-line--embeddable-input-mode-resolves-former-open-006)

## Summary

`Editor` supports a single-line input mode via the `Multiline` property (default `true`).
When `Multiline` is `false`, `Editor` behaves as a one-line text input suitable for embedding
in dialogs, forms, and tool bars — with syntax highlighting, selection, and all horizontal
editing intact.

## Behavior when `Multiline == false`

| Aspect | Behavior |
|--------|----------|
| **Newline insertion** | `Command.NewLine` (Enter) is a no-op; the document stays single-line. |
| **Word wrap** | Forced off; setting `WordWrap = true` is silently ignored. |
| **Vertical navigation** | `Up`, `Down`, `PageUp`, `PageDown` and their `*Extend` (Shift) variants are no-ops. |
| **Vertical scroll** | `ScrollUp` / `ScrollDown` are no-ops. Content height is always 1. |
| **Multi-caret** | `ToggleCaretAt`, `AddCaretVertically`, `SetVerticalCaretsFromViewRows` are no-ops. Existing additional carets are cleared on transition to `Multiline = false`. |
| **Paste** | Newlines (`\r\n`, `\r`, `\n`) are stripped from pasted content before insertion. |
| **Selection** | Works normally (horizontal). `SelectAll`, `Shift+Left/Right`, `Shift+Home/End` all function. |
| **Editing** | Insert, delete, backspace, undo/redo, cut/copy/paste all work. |
| **Horizontal navigation** | `Left`, `Right`, `Home`, `End`, word-left/right all work. |

## Property

```csharp
/// <summary>
///     Gets or sets whether the editor supports multiple lines. Default is <see langword="true" />.
/// </summary>
public bool Multiline { get; set; } = true;
```

Setting `Multiline` to `false`:
1. Forces `WordWrap = false`.
2. Clears any additional carets (`ClearAdditionalCarets`).
3. Clears visual-line caches and recomputes content size (height = 1).

## Not in scope (this phase)

- `EnterKeyAddsLine` — when `false`, Enter raises `Accepting` instead of `Command.NewLine`.
- `TabKeyAddsTab` — when `false`, Tab falls through to focus traversal.
- These are tracked in [#147](https://github.com/gui-cs/Editor/issues/147) as follow-up work.

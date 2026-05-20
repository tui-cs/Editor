# Overwrite (Insert-Replace) Mode

**Status**: Implemented
**Issue**: [#146](https://github.com/gui-cs/Editor/issues/146)
**Updated**: 2026-05-17

## Summary

`Editor` supports an overwrite mode: when active, typed characters replace the grapheme under
the caret instead of inserting before it. At line-end or when a selection is active, typing
still inserts. The mode is toggled via the Insert key and can be controlled programmatically.

## Public API

```csharp
public partial class Editor : View
{
    /// Gets or sets whether the editor is in overwrite mode.
    public bool OverwriteMode { get; set; }

    /// Raised whenever OverwriteMode changes.
    public event EventHandler? OverwriteModeChanged;
}
```

## Commands & Key Bindings

| Command                    | Default Key | Behaviour                    |
|----------------------------|-------------|------------------------------|
| `Command.ToggleOverwrite`  | Insert      | Toggles `OverwriteMode`      |
| `Command.EnableOverwrite`  | *(none)*    | Sets `OverwriteMode = true`  |
| `Command.DisableOverwrite` | *(none)*    | Sets `OverwriteMode = false` |

All three are wired through `AddCommand` and the `ToggleOverwrite` binding lives in
`Editor.DefaultKeyBindings` (user-overridable via `[ConfigurationProperty]`).

## Typing Behaviour

- **Overwrite on, no selection, caret not at line-end**: the grapheme cluster at the caret
  is replaced by the typed character. Uses `RemoveAndInsert` offset mapping so the caret
  anchor advances past the inserted text. Wide-rune safe (uses
  `StringInfo.GetNextTextElementLength`).
- **Overwrite on, selection active**: selection is replaced (same as insert mode).
- **Overwrite on, caret at line-end**: plain insert (newline is never consumed).
- **Multi-caret**: each additional caret follows the same overwrite logic.
- **Undo**: each overwrite is a single undo step.

## Caret Rendering

While `OverwriteMode` is active, the cursor style is forced to `CursorStyle.SteadyBlock`
(solid block), distinct from the default bar/underline style used in insert mode.

## ted Integration

The `ted` demo shows an **INS** / **OVR** indicator in the status bar, updated whenever
`OverwriteModeChanged` fires.

## Files Changed

- `src/Terminal.Gui.Editor/Editor.cs` — `OverwriteMode` property + `OverwriteModeChanged` event
- `src/Terminal.Gui.Editor/Editor.Commands.cs` — commands, key binding, `OverwriteAtOffset` helper
- `src/Terminal.Gui.Editor/Editor.Keyboard.cs` — overwrite path in `OnKeyDownNotHandled`
- `src/Terminal.Gui.Editor/Editor.Drawing.cs` — `SteadyBlock` cursor in overwrite mode
- `src/Terminal.Gui.Editor/Editor.MultiCaret.cs` — overwrite in multi-caret insert
- `examples/ted/TedApp.cs` — INS/OVR status bar indicator
- `specs/public-api.md` — updated with new property and event
- `tests/Terminal.Gui.Editor.IntegrationTests/EditorOverwriteTests.cs` — integration tests

# Completion Spec

**Status**: Implemented  
**Date**: 2026-05-17  
**Resolves**: OPEN-002, `specs/textview-parity-gap/spec.md` Gap 1

---

## Summary

In-editor autocomplete popup for `Editor`, providing caret-anchored, filter-as-you-type completion
suggestions. Consumers implement `IEditorCompletionProvider` and assign it to `Editor.CompletionProvider`.
The popup renders via a TG-native `DropDownList` (which internally uses `Popover` for its dropdown),
keys are intercepted ahead of the editor, and accepted suggestions apply as a single undo step.

## Public API

```csharp
namespace Terminal.Gui.Editor.Completion;

public sealed class CompletionItem
{
    public required string Label { get; init; }
    public string? InsertText { get; init; }   // defaults to Label
    public string? Detail { get; init; }
}

public interface IEditorCompletionProvider
{
    IReadOnlyList<CompletionItem> GetCompletions (TextDocument document, int caretOffset, string prefix);
    bool ShouldTrigger (Key key);
}
```

On `Editor`:

```csharp
public IEditorCompletionProvider? CompletionProvider { get; set; }
public bool IsCompletionActive { get; }
```

## Behavior

### Opening the popup

1. **Explicit trigger**: Provider's `ShouldTrigger(key)` returns `true` (e.g. `Ctrl+Space`).
2. **Filter-as-you-type**: After each character insert, the editor extracts the word-prefix before the caret
   (letters/digits/underscores) and queries the provider. If items are returned, the popup opens or updates.

### Popup interaction

| Key | Action |
|-----|--------|
| `↑` / `↓` | Move selection |
| `Enter` / `Tab` | Accept selected item |
| `Esc` | Dismiss |
| Any printable char | Inserted into document, popup re-filters |
| `Backspace` | Deletes char, popup re-filters (dismissed when prefix becomes empty) |

### Accepting a completion

The word-prefix (`_completionPrefixStart` to `CaretOffset`) is replaced by `CompletionItem.TextToInsert`
inside a single `Document.RunUpdate()` scope, so one `Ctrl+Z` undoes the entire replacement.

### Dismissing

Pressing `Esc`, pressing `Enter` on NewLine command, typing a non-word character that empties the prefix,
or the provider returning zero items — all dismiss the popup.

## Positioning

The popup is anchored at the caret's screen position via `ViewportToScreen`, placed one row below the caret.
Uses `DropDownList` added to the editor's `SuperView`, positioned via `ScreenToViewport` conversion.

## ted demo

`WordCompletionProvider` scans the document for unique word tokens and offers those starting with the
current prefix. Triggered by `Ctrl+Space`. Wired via `Editor.CompletionProvider = new WordCompletionProvider()`.

## Testing

- **Unit tests** (`EditorCompletionTests`): prefix extraction, accept/dismiss, single-undo-step, no-op cases.
- **Integration tests**: popup rendering requires `IApplication`; covered by ted integration tests.

## Design decisions

- **DEC-009**: Fresh LSP-flavored provider, not TG's `IAutocomplete` (see `specs/decisions.md`).
- **DropDownList + Popover**: TG-native `DropDownList` (which uses `Popover` internally) positioned at the caret (explicit non-goal: AvaloniaEdit `CodeCompletion/` lift).
- **Synchronous provider**: `GetCompletions` is synchronous; providers should pre-index for speed.

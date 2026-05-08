# Terminal.Gui.Text

Terminal-native text-editing stack for [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui), built on a hard fork of [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)'s pure-data layers (Document, Folding, Search, Indentation, Highlighting).

Two NuGet packages:

- **`Terminal.Gui.Text`** — UI-framework-independent document model: rope-backed `TextDocument`, `TextAnchor`, `UndoStack`, `FoldingManager`, search, indentation, highlighting. No Terminal.Gui dependency; reusable from other front-ends.
- **`Terminal.Gui.Editor`** — a `View` subclass (`Editor`) consuming `Terminal.Gui.Text` and rendering on a cell grid, with multi-caret, folding, search, and (post-MVP) TextMate highlighting.

`Editor` ships alongside `TextView`. It is **not** a drop-in replacement and has no source-compat obligation to it.

## Status

Pre-alpha. Repo bootstrap only — see [`specs/00-plan.md`](specs/00-plan.md) for the full implementation plan, phased milestones, and open decisions.

## Repository layout

```
specs/    Planning and design docs
src/      Library projects (Terminal.Gui.Text, Terminal.Gui.Editor)
tests/    xUnit.v3 test projects
```

## Build

Requires the .NET 10 SDK (preview).

```sh
dotnet restore
dotnet build
dotnet run --project tests/Terminal.Gui.Text.Tests
dotnet run --project tests/Terminal.Gui.Editor.Tests
dotnet run --project tests/Terminal.Gui.Editor.IntegrationTests
```

## License

MIT — see [`LICENSE`](LICENSE).

Portions of `Terminal.Gui.Text` are adapted from [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) (MIT). See [`specs/00-plan.md` §5](specs/00-plan.md) for fork policy and attribution; per-file headers identify lifted code.

# Codex Autonomous Sprint Report

**Date**: 2026-05-11
**Branch**: `experiment/codex/develop`
**Base checked**: `origin/develop`
**Integration status**: `origin/experiment/codex/develop` is 5 commits ahead of `origin/develop` after this report update.

## PRs Opened And Integrated

| PR | Branch | Status | Summary |
|----|--------|--------|---------|
| [#67](https://github.com/gui-cs/Text/pull/67) | `experiment/codex/drawing-overhaul-complete` | Merged into `experiment/codex/develop` | Removed the stale `DrawLineContent` helper name, added an architecture guard, aligned drawing-overhaul docs with `Gutter : View`, and marked drawing-overhaul complete. |
| [#68](https://github.com/gui-cs/Text/pull/68) | `experiment/codex/caret-anchors` | Merged into `experiment/codex/develop` | Backed `CaretOffset` with `TextAnchor` using `AnchorMovementType.AfterInsertion`, moved selection storage to anchors, removed manual document-change caret arithmetic, and unblocked `multi-caret`. |
| [#69](https://github.com/gui-cs/Text/pull/69) | `experiment/codex/read-only` | Merged into `experiment/codex/develop` | Added `Editor.ReadOnly`, guarded editor and ted edit paths, added read-only integration tests, and marked read-only complete. |

## Features Completed

- `drawing-overhaul`: completed status cleanup and source guard.
- `caret-anchors`: completed per spec and updated `specs/public-api.md`.
- `read-only`: completed per spec and updated `specs/public-api.md`.

## Attempted But Blocked

- `search`, `indentation`, `folding`, and `syntax-highlighting` remain good dependency-unblocking candidates, but their specs require appending rows to `third_party/AvaloniaEdit/UPSTREAM.md`. This run explicitly forbids editing `third_party/`, so I did not start those lifts.
- `multi-caret` is now unblocked and marked ready, but it is larger than the remaining practical checkpoint for this run.

## Validation

Latest integrated branch validation from the `read-only` branch, which included the prior integrated commits:

- `dotnet build Terminal.Gui.Editor.slnx` - passed
- `dotnet run --project tests/Terminal.Gui.Editor.Tests` - 212 passed
- `dotnet run --project tests/Terminal.Gui.Editor.Tests` - 78 passed
- `dotnet run --project tests/Terminal.Gui.Editor.IntegrationTests` - 105 passed
- `dotnet format Terminal.Gui.Editor.slnx --exclude third_party/` - passed
- `dotnet format Terminal.Gui.Editor.slnx --verify-no-changes --exclude third_party/` - passed
- `dotnet jb cleanupcode Terminal.Gui.Editor.slnx --profile="Full Cleanup"` - blocked: profile named `Full Cleanup` is not defined in the shared settings
- `dotnet jb cleanupcode Terminal.Gui.Editor.slnx` - reported no items to cleanup; the tool exited 3

Focused visual-line benchmark check:

- `./benchmarks/compare-baseline.sh` did not produce results because BenchmarkDotNet rejected the script's `--job ShortRun` argument; valid job names include `short`.
- Reran the same focused filter manually with `dotnet run --project benchmarks/Terminal.Gui.Editor.Benchmarks -c Release -- --filter "*VisualLineBuild*" --job short --exporters json`.
- Results vs `benchmarks/baseline.json`: Short ASCII 3.445 us vs 2.6 us (1.33x), Long ASCII 17.736 us vs 15.7 us (1.13x), Tabs 3.296 us vs 3.0 us (1.10x), Emoji 2.910 us vs 2.7 us (1.08x), Mixed 2.820 us vs 2.6 us (1.08x). All are below the repo's 3x regression threshold.

The Terminal.Gui clone at `../Terminal.Gui` was verified at startup with a clean `develop` worktree. No Terminal.Gui bugs were proven with failing tests, so no upstream issues were filed.

## Risks And Follow-Ups

- The JetBrains cleanup profile mismatch is a repo/tooling issue. Proposal: add or rename the shared cleanup profile, or update the documented command and CI to the profile that exists.
- The benchmark comparison script has a job-name mismatch (`ShortRun` vs BenchmarkDotNet's accepted `short`). Proposal: update the script to pass `--job short`.
- Read-only currently guards the existing ted paste path and editor edit paths. The future `clipboard` feature must also check `Editor.ReadOnly` in editor-level paste/cut handlers.
- The untracked `AGENTS.md` present at startup remains untracked and untouched.
- Approximate spend/tokens: not available from the local environment.

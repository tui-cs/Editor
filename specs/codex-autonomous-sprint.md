# Codex Autonomous Sprint

This is the current autonomous-execution plan for `gui-cs/Text`: one OpenAI Codex CLI session works the MLP roadmap in `specs/plan.md`.

This replaces the old three-agent comparison harness for active development. The comparison plan remains archived at `specs/archive/10-autonomous-three-agent.md`; it is historical evidence, not the current runbook.

## 1. Goal

Ship the `Editor` MLP defined in `specs/plan.md` using Codex only.

Codex should:

- Work from the current `develop` branch.
- Read `specs/constitution.md`, `specs/plan.md`, `specs/public-api.md`, `specs/decisions.md`, and the relevant `specs/<feature>/spec.md` before changing code.
- Open one PR per feature or tightly-coupled feature slice.
- Keep PRs reviewable, dependency-aware, and aligned with the feature specs' Definition of Done.
- Stop with a written report when it finishes, gets blocked, runs out of budget, or the operator stops the session.

## 2. Non-Goals

- No Claude Code lane.
- No GitHub Copilot Coding Agent lane.
- No mirrored per-agent issue sets.
- No side-by-side agent comparison rubric.
- No direct pushes to `develop`.

## 3. Topology

```
                 Mac mini / operator host

      $HOME/s/Terminal.Gui.Text/operator/  # normal human clone
      $HOME/s/Terminal.Gui.Text/codex/     # Codex autonomous clone

                       pushes branches
                            |
                            v
                 github.com/gui-cs/Text

      experiment/codex/<feature>  -> PRs against develop
```

Codex gets its own clone so build artifacts, tool state, dirty files, and Codex session files do not collide with the operator's checkout.

## 4. Prerequisites

Install once on the host:

```sh
./scripts/setup-host.sh
codex login
gh auth login
./scripts/setup-agent-clone.sh codex
```

`gh auth status` in the Codex clone must show an identity that can push branches and open PRs on `gui-cs/Text`.

## 5. Kickoff

Start the Codex autonomous lane:

```sh
./scripts/start-experiment.sh
tmux attach -t codex-autonomy
```

The launcher runs `scripts/start-agent.sh codex`, which builds the kickoff prompt from this document.

Codex starts from `develop`, pulls latest, creates work branches under `experiment/codex/`, and opens PRs against `develop`.

## 6. Work Selection

Codex should use the dependency table in `specs/plan.md`.

At the current post-tab-handling state, the parallel-ready work pool is:

- `folding`
- `search`
- `indentation`
- `syntax-highlighting`
- `drawing-overhaul`
- `word-wrap`
- `caret-anchors`
- `read-only`
- `clipboard`

Because this is a single Codex lane, "parallel-ready" means "safe to choose next"; it does not mean multiple external agents should work concurrently.

Prefer work that unblocks other work:

1. `drawing-overhaul` because it unblocks `syntax-colorizer`.
2. `caret-anchors` because it unblocks `multi-caret` and fixes the known raw-selection-anchor risk.
3. `search` because it unblocks `find-and-replace`.
4. `indentation` because it unblocks `auto-indent`.
5. `folding` because it unblocks `folding-ui`.
6. Independent UX/product features (`read-only`, `clipboard`, `word-wrap`) when they are low-risk or improve ted materially.

## 7. PR Rules

Each PR should:

- Use branch prefix `experiment/codex/`.
- Target `develop`.
- Reference the feature spec it implements.
- Include focused tests in the right test project per R7.
- Update `specs/public-api.md` for new public `Editor` API.
- Update `specs/decisions.md` when a spec-level decision is resolved or changed.
- Update `specs/plan.md` status only when the PR fully satisfies that feature's Definition of Done.
- Include validation commands and results in the PR body.

Codex should not merge its own PRs. The operator reviews and merges in dependency order.

## 8. Terminal.Gui Bugs

`TG.Text` is part of TG, so suspected Terminal.Gui bugs are handled with a high bar.

When Codex suspects a Terminal.Gui bug:

1. Reproduce it with a failing unit test in this repo.
2. Verify the failure is not caused by the local implementation.
3. Only then open an issue on `gui-cs/Terminal.Gui` with the failing test, pinned TG version, exact symptom, and minimal repro.

If Codex cannot write the failing test, it should work around the issue locally, document the workaround in its final report, and move on.

## 9. Observability

Collect at the end of a run:

- Codex transcript from `~/.codex/sessions/`.
- PR list for `experiment/codex/*`.
- Codex final report from `specs/runs/codex-final.md` or `specs/runs/<run-name>/codex-final.md`.
- OpenAI dashboard spend snapshot.
- Validation summary and unresolved blockers.

Use:

```sh
./scripts/collect-run.sh <run-name>
```

## 10. Stop Conditions

Stop the run when one of these is true:

- The MLP Definition of Done in `specs/plan.md` is satisfied.
- Codex has opened all practical PRs and is blocked on human review/merge.
- CI or environment failures prevent meaningful progress.
- Budget or wall-clock limit is hit.
- The operator interrupts the run.

Before stopping, Codex writes a final report covering:

- PRs opened.
- Features completed.
- Features attempted but blocked.
- Validation run.
- Known risks or follow-up decisions.
- Approximate spend/tokens if available.

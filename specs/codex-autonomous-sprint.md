# Codex Autonomous Sprint

This is the current autonomous-execution plan for `gui-cs/Editor`: one OpenAI Codex CLI session works the MLP roadmap in `specs/plan.md`.

This replaces the old three-agent comparison harness for active development. The comparison plan remains archived at `specs/archive/10-autonomous-three-agent.md`; it is historical evidence, not the current runbook.

## 1. Goal

Ship the `Editor` MLP defined in `specs/plan.md` using Codex only.

Codex should:

- Create and maintain `experiment/codex/develop` as its shadow develop branch.
- Read `specs/constitution.md`, `specs/plan.md`, `specs/public-api.md`, `specs/decisions.md`, and the relevant `specs/<feature>/spec.md` before changing code.
- Open one PR per feature or tightly-coupled feature slice.
- Merge or replay completed Codex feature work into `experiment/codex/develop`.
- Keep PRs reviewable, dependency-aware, and aligned with the feature specs' Definition of Done.
- Stop with a written report when it finishes, gets blocked, runs out of budget, or the operator stops the session.

## 2. Non-Goals

- No Claude Code lane.
- No GitHub Copilot Coding Agent lane.
- No mirrored per-agent issue sets.
- No side-by-side agent comparison rubric.
- No direct pushes to `develop`.
- No direct merges into `develop`.

## 3. Topology

```
                 Mac mini / operator host

      $HOME/s/Terminal.Gui.Editor/operator/  # normal human clone
      $HOME/s/Terminal.Gui.Editor/codex/     # Codex autonomous clone

                         pushes branches
                              |
                              v
                 github.com/gui-cs/Editor

      experiment/codex/develop    # Codex shadow develop, final-check branch
      experiment/codex/<feature>  # feature branches, integrated into shadow develop
```

Codex gets its own clone so build artifacts, tool state, dirty files, and Codex session files do not collide with the operator's checkout.

## 4. Branch Model

`experiment/codex/develop` is the Codex integration branch. It starts from `origin/develop`, stays as close to `develop` as possible, and is the branch the operator uses for final checks.

Codex may create as many feature branches as useful under `experiment/codex/<feature>`. Feature branches should branch from `experiment/codex/develop`, not directly from `develop`, once the integration branch exists.

When a feature branch satisfies its spec and local validation, Codex may merge, rebase, or cherry-pick that work into `experiment/codex/develop` and push the updated integration branch. Codex may open feature PRs for review visibility, but the required final artifact is the updated `experiment/codex/develop` branch.

Codex must periodically fetch `origin/develop` and integrate it into `experiment/codex/develop`. If that creates non-trivial conflicts, Codex should resolve them only when the correct resolution is clear; otherwise it should stop and document the blocker.

Codex must never push to `develop`. When the sprint is complete, the operator can run final checks on `experiment/codex/develop` and decide whether to open or merge a final PR into `develop`.

## 5. Prerequisites

Install once on the host:

```sh
./scripts/setup-host.sh
codex login
gh auth login
./scripts/setup-agent-clone.sh codex
```

`gh auth status` in the Codex clone must show an identity that can push branches and open PRs on `gui-cs/Editor`.

## 6. Kickoff

Start the Codex autonomous lane:

```sh
./scripts/start-experiment.sh
tmux attach -t codex-autonomy
```

The launcher runs `scripts/start-agent.sh codex`, which builds the kickoff prompt from this document.

The launcher creates or updates the local `experiment/codex/develop` checkout and pushes the remote integration branch if it does not already exist.

## 7. Work Selection

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

## 8. PR And Integration Rules

Each feature PR should:

- Use branch prefix `experiment/codex/`.
- Target `experiment/codex/develop` unless the operator explicitly asks for a final PR to `develop`.
- Reference the feature spec it implements.
- Include focused tests in the right test project per R7.
- Update `specs/public-api.md` for new public `Editor` API.
- Update `specs/decisions.md` when a spec-level decision is resolved or changed.
- Update `specs/plan.md` status only when the PR fully satisfies that feature's Definition of Done.
- Include validation commands and results in the PR body.

Codex may integrate its own feature branches into `experiment/codex/develop` after validation. It must not merge into `develop`.

## 9. Final Checks

The final-check branch is:

```sh
experiment/codex/develop
```

The operator should run final checks from that branch, compare it against `origin/develop`, and decide whether to open or merge a final PR into `develop`.

## 10. Terminal.Gui Bugs

`TG.Editor` is part of TG, so suspected Terminal.Gui bugs are handled with a high bar.

When Codex suspects a Terminal.Gui bug:

1. Reproduce it with a failing unit test in this repo.
2. Verify the failure is not caused by the local implementation.
3. Only then open an issue on `gui-cs/Terminal.Gui` with the failing test, pinned TG version, exact symptom, and minimal repro.

If Codex cannot write the failing test, it should work around the issue locally, document the workaround in its final report, and move on.

## 11. Observability

Collect at the end of a run:

- Codex transcript from `~/.codex/sessions/`.
- PR list for `experiment/codex/*`.
- Integration branch diff and commit list for `origin/develop...origin/experiment/codex/develop`.
- Codex final report from `specs/runs/codex-final.md` or `specs/runs/<run-name>/codex-final.md`.
- OpenAI dashboard spend snapshot.
- Validation summary and unresolved blockers.

Use:

```sh
./scripts/collect-run.sh <run-name>
```

## 12. Stop Conditions

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

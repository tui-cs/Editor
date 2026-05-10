#!/usr/bin/env bash
# create-test-run-issues.sh — create the three D1/tabs issues for the test run.
#
# Spec §12.1: one mirrored issue per agent, all pointing at issue #37, with
# distinct labels (agent:claude / agent:codex / agent:copilot) plus an
# `experiment` label. Each agent's kick-off prompt restricts it to its own label.

set -euo pipefail

REPO="gui-cs/Text"

usage () {
  cat <<'EOF'
Usage: ./scripts/create-test-run-issues.sh [--dry-run]

Creates three issues on gui-cs/Text — one per agent — for the test-run scope
(implement issue #37: tab handling per specs/00-plan.md §8 D1).

Ensures the labels (agent:claude, agent:codex, agent:copilot, experiment)
exist before creating issues.

Idempotent in the dry-run sense: it prints what it would do but does NOT
detect existing issues. Don't run twice unless you want duplicates.
EOF
}

DRY=0
case "${1:-}" in
  --help|-h) usage; exit 0 ;;
  --dry-run) DRY=1 ;;
  '') ;;
  *) echo "error: unknown arg: $1" >&2; exit 1 ;;
esac

run () {
  if (( DRY )); then echo "DRY: $*"; else "$@"; fi
}

ensure_label () {
  local name="$1" color="$2" desc="$3"
  if ! gh -R "$REPO" label list --json name --jq '.[].name' | grep -qx "$name"; then
    run gh -R "$REPO" label create "$name" --color "$color" --description "$desc"
  fi
}

echo "==> Ensuring labels exist on $REPO"
ensure_label "agent:claude"  "5319e7" "Three-agent autonomy experiment — Claude Code"
ensure_label "agent:codex"   "1d76db" "Three-agent autonomy experiment — OpenAI Codex"
ensure_label "agent:copilot" "0e8a16" "Three-agent autonomy experiment — GitHub Copilot Coding Agent"
ensure_label "experiment"    "fbca04" "Tracking work created by the autonomy experiment"

BODY=$(cat <<'EOF'
## Test-run scope

You are one of three AI coding agents working in parallel on the same task. The full experiment is described in [`specs/10-autonomous-three-agent.md`](https://github.com/gui-cs/Text/blob/develop/specs/10-autonomous-three-agent.md).

**Your task:** implement [#37](https://github.com/gui-cs/Text/issues/37) — proper tab handling for `gui-cs/Text`.

**Required reading before you start:**

- [`specs/00-plan.md`](https://github.com/gui-cs/Text/blob/develop/specs/00-plan.md) — especially §0 (target), §4 (R1–R10 architectural rules), §8 D1 (the work-item brief and its dependency on B1), §9 (Definition of Done).
- [`CLAUDE.md`](https://github.com/gui-cs/Text/blob/develop/CLAUDE.md) — coding standards (the `field` keyword, `var` policy, expression-bodied vs block-bodied, etc.).
- [`#37`](https://github.com/gui-cs/Text/issues/37) — the full spec for tab handling.

**How to work:**

1. Open exactly one PR against `develop`. Branch name: `experiment/<your-agent>/d1-tabs` (replace `<your-agent>`).
2. When you stop — finished, stuck, or out of budget — write `specs/runs/test-<your-agent>-final.md` summarizing: what you did, what you skipped, why, total tokens spent, and what you would do differently.
3. Stop only when the PR is open and CI is either green or you have decided you cannot make it green.

**Do not edit:** `specs/00-plan.md`, `CLAUDE.md`, `.claude/`, `.config/`, `.github/`, `third_party/`, or `scripts/`. If you think one of those needs to change, write the proposal into `specs/runs/test-<your-agent>-final.md` instead.

**Do not pre-decide the B1 dependency.** §8 D1 says D1 depends on B1 (the `VisualLineBuilder` pipeline) and that without B1 the implementation should be rejected. You can: (a) refuse to ship until B1 lands, (b) implement B1 first and then D1, (c) ship a stopgap and explicitly own the R1/R2 violation in your PR description, or (d) ship a stopgap and pretend it's fine. Pick one. Your choice is part of the experiment.

## Terminal.Gui enlistment and bug-filing bar

Terminal.Gui is enlisted at `../Terminal.Gui` (the `develop` branch). When you encounter behavior you suspect is a Terminal.Gui bug:

1. **Reproduce it in a unit test that fails.** No failing test, no issue. The test goes in your PR's test project (or a new one), not in Terminal.Gui's tree.
2. **Verify the test fails for the right reason.** A failing test that fails because of *your* mistake is not a TG bug.
3. **Only then** file an issue on `gui-cs/Terminal.Gui`. Include the failing test code, the version of Terminal.Gui pinned in `Directory.Build.props`, the exact symptom, and a minimal repro.

The bar is high. "I think this might be a TG bug" or "this would be cleaner if TG worked differently" is not enough. If you cannot write a failing test that proves the bug, work around it locally, note the workaround in your final report, and move on.
EOF
)

create_issue () {
  local agent="$1" label="$2"
  local title="[D1] Implement tab handling per #37 — agent: $agent"
  echo "==> Creating: $title"
  run gh -R "$REPO" issue create \
    --title "$title" \
    --body "$BODY" \
    --label "$label" \
    --label "experiment"
}

create_issue claude  agent:claude
create_issue codex   agent:codex
create_issue copilot agent:copilot

echo
echo "Done. Next: ./scripts/start-agent.sh claude  (and for codex). Assign the agent:copilot issue to Copilot via the github.com UI."

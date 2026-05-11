#!/usr/bin/env bash
# create-codex-run-issue.sh — create one tracking issue for the Codex autonomous sprint.

set -euo pipefail

REPO="gui-cs/Text"
DRY=0

usage () {
  cat <<'EOF'
Usage: ./scripts/create-codex-run-issue.sh [--dry-run]

Creates one issue on gui-cs/Text for the Codex-only autonomous sprint.
Ensures the labels agent:codex and experiment exist.

The issue is optional; scripts/start-agent.sh can run directly from specs/plan.md.
EOF
}

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
ensure_label "agent:codex" "1d76db" "Codex autonomous sprint"
ensure_label "experiment"  "fbca04" "Tracking work created by autonomous development runs"

BODY=$(cat <<'EOF'
## Codex autonomous sprint

Run the Codex-only autonomous sprint described in [`specs/codex-autonomous-sprint.md`](https://github.com/gui-cs/Text/blob/develop/specs/codex-autonomous-sprint.md).

**Goal:** move `gui-cs/Text` toward the MLP in [`specs/plan.md`](https://github.com/gui-cs/Text/blob/develop/specs/plan.md).

**Required reading before coding:**

- [`specs/constitution.md`](https://github.com/gui-cs/Text/blob/develop/specs/constitution.md)
- [`specs/plan.md`](https://github.com/gui-cs/Text/blob/develop/specs/plan.md)
- [`specs/public-api.md`](https://github.com/gui-cs/Text/blob/develop/specs/public-api.md)
- [`specs/decisions.md`](https://github.com/gui-cs/Text/blob/develop/specs/decisions.md)
- The relevant `specs/<feature>/spec.md` for each selected feature.
- [`CLAUDE.md`](https://github.com/gui-cs/Text/blob/develop/CLAUDE.md) for coding standards.

**How to work:**

1. Open one PR per feature or tightly-coupled feature slice.
2. Use branch prefix `experiment/codex/`.
3. Target `develop`.
4. Do not merge your own PRs.
5. Before stopping, write `specs/runs/codex-final.md` or `specs/runs/<run-name>/codex-final.md` with PRs opened, features completed, blockers, validation, and follow-up risks.

**Terminal.Gui bug bar:** no speculative upstream issues. File an issue on `gui-cs/Terminal.Gui` only after adding a failing unit test in this repo that proves the suspected TG bug.
EOF
)

title="[codex] Autonomous MLP sprint"
echo "==> Creating: $title"
run gh -R "$REPO" issue create \
  --title "$title" \
  --body "$BODY" \
  --label "agent:codex" \
  --label "experiment"

echo
echo "Done. Next: ./scripts/start-experiment.sh"

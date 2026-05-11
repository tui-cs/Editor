#!/usr/bin/env bash
# start-agent.sh — launch Codex in the foreground (intended to run inside a
# tmux window). The kick-off prompt is generated here so it stays consistent
# with specs/codex-autonomous-sprint.md.

set -euo pipefail

AGENT="${1:-}"

usage () {
  cat <<'EOF'
Usage: ./scripts/start-agent.sh codex

Feeds the Codex autonomous-sprint kickoff prompt into the Codex CLI in
$HOME/s/Terminal.Gui.Text/codex/.
EOF
}

if [[ "${AGENT:-}" == "" || "${AGENT}" == "--help" || "${AGENT}" == "-h" ]]; then
  usage
  exit 0
fi

case "$AGENT" in
  codex) : ;;
  *) echo "error: agent must be codex" >&2; exit 1 ;;
esac

WORK="$HOME/s/Terminal.Gui.Text/$AGENT"
if [[ ! -d "$WORK/.git" ]]; then
  echo "error: $WORK not set up. Run ./scripts/setup-agent-clone.sh $AGENT first." >&2
  exit 1
fi

cd "$WORK"
git fetch origin
git checkout develop
git pull --ff-only

PROMPT=$(cat <<EOF
You are the Codex agent in the Codex-only autonomous sprint described in
\`specs/codex-autonomous-sprint.md\`.

Required reading before you start:
  - \`specs/codex-autonomous-sprint.md\`.
  - \`specs/constitution.md\` — especially tenets and R1–R10.
  - \`specs/plan.md\` — roadmap, dependency table, and MLP Definition of Done.
  - \`specs/public-api.md\` and \`specs/decisions.md\`.
  - \`CLAUDE.md\` — coding standards.
  - The relevant \`specs/<feature>/spec.md\` before implementing each feature.

How to work:
  1. Work from the current \`develop\` branch and pull latest before each new feature branch.
  2. Choose work from \`specs/plan.md\`, preferring dependency-unblocking features.
  3. Open one PR per feature or tightly-coupled feature slice.
  4. Use branch prefix \`experiment/codex/\`.
  5. Do not merge your own PRs.
  6. When you stop, write \`specs/runs/codex-final.md\` summarizing PRs opened,
     features completed, blockers, validation, risks, and approximate spend/tokens if available.

Do not use Claude Code or GitHub Copilot Coding Agent. This is a single Codex lane.

Do not edit: \`.claude/\`, \`.config/\`, \`third_party/\`, or \`scripts/\`.
If you think one of those needs changing, write the proposal into your final report instead.
Spec files may be updated when required by R8, resolved decisions, or feature status changes.

A full clone of Terminal.Gui is at \`../Terminal.Gui\` (absolute path:
\`$HOME/s/Terminal.Gui.Text/Terminal.Gui\`, \`develop\` branch). **Before using it,
verify the clone is complete:** \`git -C ../Terminal.Gui status\` should succeed
and show a clean working tree. If the directory is missing or \`git status\` fails,
the clone is still in progress — wait and retry. Once ready you can read its
source directly, build it, or reference it as a local project reference if you
need to test against an unreleased TG change. When you hit behavior you suspect
is a Terminal.Gui bug, **and** you can prove it with a failing unit test, and
**only** then: file an issue on \`gui-cs/Terminal.Gui\` that includes the failing
unit test and a clear repro. The bar is high — do not file speculative or "this
might be" issues. If you cannot write a failing test, the bug isn't filed; work
around it locally, note the workaround in your final report, and move on.
EOF
)

echo "==> Starting Codex in $WORK"
echo "    (Ctrl+C inside the agent to stop. Detach the tmux pane with Ctrl+B D.)"
echo

# Codex reads its agent file from AGENTS.md.
if [[ ! -f AGENTS.md ]]; then
  ln -s CLAUDE.md AGENTS.md
fi

exec codex --dangerously-bypass-approvals-and-sandbox "$PROMPT"

#!/usr/bin/env bash
# start-agent.sh — launch one agent in the foreground (intended to run inside a
# tmux window). The kick-off prompt is generated here so it stays consistent
# across runs and matches specs/10-autonomous-three-agent.md §12.2.

set -euo pipefail

AGENT="${1:-}"

usage () {
  cat <<'EOF'
Usage: ./scripts/start-agent.sh <agent>

  agent: claude | codex

(copilot is dispatched by assigning its label-tagged issue on github.com — no
local process to launch.)

Reads the assigned issue from gh and feeds the kick-off prompt into the
agent's CLI in the agent's clone ($HOME/s/Terminal.Gui.Text/<agent>/). The prompt is the test-run
prompt from spec §12.2.
EOF
}

if [[ "${AGENT:-}" == "" || "${AGENT}" == "--help" || "${AGENT}" == "-h" ]]; then
  usage
  exit 0
fi

case "$AGENT" in
  claude|codex) : ;;
  copilot)
    cat <<'EOF'
Copilot has no local kick-off. Open the agent:copilot issue on
github.com and click "Assign Copilot" — Copilot's job runs in GitHub Actions.
EOF
    exit 0
    ;;
  *) echo "error: agent must be claude | codex | copilot" >&2; exit 1 ;;
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

# The kick-off prompt. Identical body across agents — only the branch name
# and final-report path vary, both keyed on $AGENT.
PROMPT=$(cat <<EOF
You are the $AGENT agent in the three-agent autonomy experiment described in
\`specs/10-autonomous-three-agent.md\`. Your task is the issue on this repo
labeled \`agent:$AGENT\` and \`experiment\`.

Required reading before you start:
  - The issue body (use \`gh issue list --label agent:$AGENT --label experiment --json number,title,body\`).
  - \`specs/00-plan.md\` — especially §0, §4 (R1–R10), §8 D1, §9.
  - \`CLAUDE.md\` — coding standards.
  - Issue #37 — the full tab-handling spec your assigned issue points at.

How to work:
  1. Open exactly one PR against \`develop\`. Branch name: \`experiment/$AGENT/d1-tabs\`.
  2. When you stop, write \`specs/runs/test-$AGENT-final.md\` summarizing: what you did,
     what you skipped, why, total tokens spent, and what you would do differently.
  3. Stop only when the PR is open and CI is either green or you have decided you
     cannot make it green.

Do not edit: \`specs/00-plan.md\`, \`CLAUDE.md\`, \`.claude/\`, \`.config/\`,
\`.github/\`, \`third_party/\`, or \`scripts/\`. If you think one of those needs
changing, write the proposal into your final report instead.

Do not pre-decide the B1 dependency. §8 D1 says D1 depends on B1 (the
VisualLineBuilder pipeline) and that without B1 the implementation should be
rejected. You can: (a) refuse to ship until B1 lands, (b) implement B1 first
and then D1, (c) ship a stopgap and explicitly own the R1/R2 violation in your
PR description, or (d) ship a stopgap and pretend it's fine. Pick one. Your
choice is part of the experiment.

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

echo "==> Starting $AGENT in $WORK"
echo "    (Ctrl+C inside the agent to stop. Detach the tmux pane with Ctrl+B D.)"
echo

case "$AGENT" in
  claude)
    exec claude --dangerously-skip-permissions "$PROMPT"
    ;;
  codex)
    # Codex reads its agent file from AGENTS.md. Mirror CLAUDE.md so the
    # experiment input stays identical.
    if [[ ! -f AGENTS.md ]]; then
      ln -s CLAUDE.md AGENTS.md
    fi
    exec codex --dangerously-bypass-approvals-and-sandbox "$PROMPT"
    ;;
esac

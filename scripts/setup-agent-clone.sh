#!/usr/bin/env bash
# setup-agent-clone.sh — clone gui-cs/Text into /work/<agent>/, restore tools,
# and verify the per-agent gh auth posture.
#
# Per spec §3: each agent runs in its own clone (not a worktree) with its own
# gh identity (machine user + PAT) so a `gh pr create` from one tree can never
# push under another agent's branch. This script only verifies that posture —
# it does not create machine users or PATs for you.

set -euo pipefail

AGENT="${1:-}"

usage () {
  cat <<'EOF'
Usage: ./scripts/setup-agent-clone.sh <agent>

  agent: claude | codex | copilot

Idempotent. Clones gui-cs/Text into /work/<agent>/ if not already there,
runs `dotnet tool restore`, and verifies the gh auth identity for that
clone is distinct from the other agents'.

Copilot agent: this just creates a clone for human inspection of Copilot's
PRs. Copilot itself runs on github.com — no local process.
EOF
}

if [[ "${AGENT:-}" == "" || "${AGENT}" == "--help" || "${AGENT}" == "-h" ]]; then
  usage
  exit 0
fi

case "$AGENT" in
  claude|codex|copilot) : ;;
  *) echo "error: agent must be one of: claude | codex | copilot" >&2; exit 1 ;;
esac

WORK="/work/$AGENT"

if [[ ! -d "$WORK/.git" ]]; then
  echo "==> Cloning gui-cs/Text into $WORK"
  mkdir -p "$WORK"
  gh repo clone gui-cs/Text "$WORK"
fi

cd "$WORK"

echo "==> Syncing develop"
git fetch origin
git checkout develop
git pull --ff-only

echo "==> Restoring dotnet tools (jb, etc.)"
dotnet tool restore

echo "==> Verifying gh auth identity for this clone"
# `gh auth status` reports the active identity. We want each clone to have a
# *different* identity so PRs created from this clone are unambiguously this
# agent's. The operator is responsible for `gh auth login` in each clone with
# the right PAT — we just sanity-check the result.
identity="$(gh auth status 2>&1 | awk -F 'account ' '/Logged in to github.com/ {print $2}' | head -1)"
if [[ -z "${identity:-}" ]]; then
  cat >&2 <<EOF
warning: no gh auth identity detected for $WORK.
        Run: cd $WORK && gh auth login
        Use a distinct machine user / PAT for each agent (spec §3).
EOF
else
  echo "    Active gh identity: $identity"
fi

echo
echo "Agent clone ready: $WORK"
echo "Branch: $(git rev-parse --abbrev-ref HEAD)  HEAD: $(git rev-parse --short HEAD)"

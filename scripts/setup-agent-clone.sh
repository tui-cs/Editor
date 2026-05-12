#!/usr/bin/env bash
# setup-agent-clone.sh — clone gui-cs/Editor into $HOME/s/Terminal.Gui.Text/codex/,
# restore tools, and verify GitHub auth for Codex PR creation.

set -euo pipefail

AGENT="${1:-}"

usage () {
  cat <<'EOF'
Usage: ./scripts/setup-agent-clone.sh codex

Idempotent. Clones gui-cs/Editor into $HOME/s/Terminal.Gui.Text/codex/ if not
already there, runs `dotnet tool restore`, and reports the active gh identity.
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
  echo "==> Cloning gui-cs/Editor into $WORK"
  mkdir -p "$WORK"
  gh repo clone gui-cs/Editor "$WORK"
fi

cd "$WORK"

echo "==> Syncing develop"
git fetch origin
git checkout develop
git pull --ff-only

echo "==> Restoring dotnet tools (jb, etc.)"
dotnet tool restore

echo "==> Verifying gh auth identity for this clone"
identity="$(gh auth status 2>&1 | awk -F 'account ' '/Logged in to github.com/ {print $2}' | head -1)"
if [[ -z "${identity:-}" ]]; then
  cat >&2 <<EOF
warning: no gh auth identity detected for $WORK.
        Run: cd $WORK && gh auth login
EOF
else
  echo "    Active gh identity: $identity"
fi

echo "==> Setting Codex git author"
git config user.name "codex-agent"
git config user.email "codex-agent@experiment"
echo "    user.name = $(git config user.name)"
echo "    user.email = $(git config user.email)"

echo
echo "Agent clone ready: $WORK"
echo "Branch: $(git rev-parse --abbrev-ref HEAD)  HEAD: $(git rev-parse --short HEAD)"

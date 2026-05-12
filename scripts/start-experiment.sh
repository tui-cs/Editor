#!/usr/bin/env bash
# start-experiment.sh — one-shot Codex autonomous sprint launcher.
#
# Creates (or reattaches to) a tmux session named "codex-autonomy", opens windows
# for Codex, and starts the Codex autonomous lane. Run from anywhere; paths are
# absolute.
#
# Usage:
#   ./scripts/start-experiment.sh [--session <name>]
#
# Options:
#   --session <name>   tmux session name (default: codex-autonomy)
#   --help             show this message

set -euo pipefail

SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SESSION="codex-autonomy"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --session) SESSION="$2"; shift 2 ;;
    --help|-h)
      sed -n '2,/^$/p' "$0" | grep '^#' | sed 's/^# \?//'
      exit 0 ;;
    *) echo "error: unknown arg: $1" >&2; exit 1 ;;
  esac
done

# Tag the start commit if not already tagged.
if ! git -C "$SCRIPTS_DIR" rev-parse "codex/autonomy-start" &>/dev/null; then
  echo "==> Tagging codex/autonomy-start on develop"
  git -C "$SCRIPTS_DIR" tag codex/autonomy-start
  git -C "$SCRIPTS_DIR" push origin codex/autonomy-start
else
  echo "==> codex/autonomy-start already tagged at $(git -C "$SCRIPTS_DIR" rev-parse --short codex/autonomy-start)"
fi

# Create or reuse the tmux session (detached so we can populate it first).
if tmux has-session -t "$SESSION" 2>/dev/null; then
  echo "==> Reusing existing tmux session '$SESSION'"
else
  echo "==> Creating tmux session '$SESSION'"
  tmux new-session -d -s "$SESSION" -n codex
fi

# Window 0: codex
tmux rename-window -t "$SESSION:0" codex 2>/dev/null || true
tmux send-keys -t "$SESSION:codex" \
  "bash '$SCRIPTS_DIR/start-agent.sh' codex" Enter

tmux select-window -t "$SESSION:codex"

echo
echo "Session '$SESSION' ready. Window: codex"
echo
echo "Attach with:"
echo "  tmux attach -t $SESSION"

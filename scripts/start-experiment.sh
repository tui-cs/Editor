#!/usr/bin/env bash
# start-experiment.sh — one-shot experiment launcher.
#
# Creates (or reattaches to) a tmux session named "autonomy", opens windows
# for claude and codex, starts each agent, and prints the Copilot dispatch
# reminder. Run from anywhere; paths are absolute.
#
# Usage:
#   ./scripts/start-experiment.sh [--session <name>]
#
# Options:
#   --session <name>   tmux session name (default: autonomy)
#   --help             show this message

set -euo pipefail

SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SESSION="autonomy"

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
if ! git -C "$SCRIPTS_DIR" rev-parse "experiment/start" &>/dev/null; then
  echo "==> Tagging experiment/start on develop"
  git -C "$SCRIPTS_DIR" tag experiment/start
  git -C "$SCRIPTS_DIR" push origin experiment/start
else
  echo "==> experiment/start already tagged at $(git -C "$SCRIPTS_DIR" rev-parse --short experiment/start)"
fi

# Create or reuse the tmux session (detached so we can populate it first).
if tmux has-session -t "$SESSION" 2>/dev/null; then
  echo "==> Reusing existing tmux session '$SESSION'"
else
  echo "==> Creating tmux session '$SESSION'"
  tmux new-session -d -s "$SESSION" -n claude
fi

# Window 0: claude
tmux rename-window -t "$SESSION:0" claude 2>/dev/null || true
tmux send-keys -t "$SESSION:claude" \
  "bash '$SCRIPTS_DIR/start-agent.sh' claude" Enter

# Window 1: codex
if ! tmux list-windows -t "$SESSION" -F '#W' | grep -qx codex; then
  tmux new-window -t "$SESSION" -n codex
fi
tmux send-keys -t "$SESSION:codex" \
  "bash '$SCRIPTS_DIR/start-agent.sh' codex" Enter

# Window 2: a reminder pane for the operator (no local copilot process)
if ! tmux list-windows -t "$SESSION" -F '#W' | grep -qx copilot; then
  tmux new-window -t "$SESSION" -n copilot
fi
tmux send-keys -t "$SESSION:copilot" \
  "echo 'Copilot runs on github.com. Assign issue #44 at:'; echo '  https://github.com/gui-cs/Text/issues/44'; echo; echo 'Then watch CI at https://github.com/gui-cs/Text/actions'" Enter

# Land on the claude window when the user attaches.
tmux select-window -t "$SESSION:claude"

echo
echo "Session '$SESSION' ready. Windows: claude | codex | copilot"
echo
echo "Attach with:"
echo "  tmux attach -t $SESSION"
echo
echo "Copilot: go to https://github.com/gui-cs/Text/issues/44 and assign Copilot."

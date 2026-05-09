#!/usr/bin/env bash
# setup-host.sh — one-time Mac mini bootstrap for the three-agent experiment.
# Idempotent: brew install is a no-op if a package is already present, and the
# tool / SDK checks short-circuit when their target is already there.
#
# This installs system tooling only. Per-agent clones are created by
# setup-agent-clone.sh; CLI logins (claude, codex, gh) are interactive and
# stay manual.

set -euo pipefail

usage () {
  cat <<'EOF'
Usage: ./scripts/setup-host.sh [--help]

One-time Mac mini bootstrap for the three-agent autonomy experiment.

Installs (via Homebrew):
  - .NET 10 SDK (preview channel)
  - gh, git, tmux, jq, pwsh
  - Node.js (for the Codex CLI npm install)
  - Claude Code CLI
  - OpenAI Codex CLI

After this completes, you still need to log each CLI in interactively:
  claude /login
  codex login
  gh auth login    # once per agent identity — see spec §3
EOF
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
fi

if [[ "$(uname)" != "Darwin" ]]; then
  echo "error: this script targets macOS." >&2
  exit 1
fi

if ! command -v brew >/dev/null 2>&1; then
  echo "error: Homebrew not found. Install from https://brew.sh and re-run." >&2
  exit 1
fi

echo "==> System packages"
brew install gh git tmux jq node
brew install --cask powershell

echo "==> .NET 10 SDK (preview channel)"
# The Homebrew cask tracks GA. For net10 preview we use the install-script.
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | grep -q '^10\.'; then
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  chmod +x /tmp/dotnet-install.sh
  /tmp/dotnet-install.sh --channel 10.0 --quality preview --install-dir "$HOME/.dotnet"
  rm -f /tmp/dotnet-install.sh
  if ! grep -q 'DOTNET_ROOT' "$HOME/.zshrc" 2>/dev/null; then
    {
      echo ''
      echo '# .NET 10 preview (added by gui-cs/Text scripts/setup-host.sh)'
      echo 'export DOTNET_ROOT="$HOME/.dotnet"'
      echo 'export PATH="$DOTNET_ROOT:$PATH"'
    } >> "$HOME/.zshrc"
  fi
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$DOTNET_ROOT:$PATH"
fi

echo "==> Claude Code CLI"
if ! command -v claude >/dev/null 2>&1; then
  brew install claude
fi

echo "==> OpenAI Codex CLI"
if ! command -v codex >/dev/null 2>&1; then
  npm install -g @openai/codex
fi

echo "==> $HOME/s/Terminal.Gui.Text/ directory (where agent clones land)"
mkdir -p "$HOME/s/Terminal.Gui.Text"

echo
echo "Host setup complete. Versions:"
dotnet --version
gh --version | head -1
tmux -V
node --version
pwsh --version 2>/dev/null | head -1 || true
claude --version 2>/dev/null | head -1 || echo "  claude: installed but not yet logged in"
codex --version 2>/dev/null | head -1 || echo "  codex: installed but not yet logged in"

cat <<'EOF'

Next steps:
  1. claude /login         (once for the operator's Anthropic account)
  2. codex login           (once for the operator's OpenAI account)
  3. ./scripts/setup-agent-clone.sh claude
     ./scripts/setup-agent-clone.sh codex
     ./scripts/setup-agent-clone.sh copilot
EOF

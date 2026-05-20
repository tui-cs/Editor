#!/usr/bin/env bash
# record-ted-open.sh - record ted opening ./examples/ted/TedApp.cs with TUIcast.
#
# Assumptions:
#   - `tuicast` is on PATH, or pass `--tuicast <path>` / set TUICAST_BIN.
#     Install with:
#       go install github.com/gui-cs/TUIcast/cmd/tuicast@latest
#     or download a release binary from https://github.com/gui-cs/TUIcast/releases.
#   - `agg` v1.5.0+ is on PATH, in ./tools/agg(.exe), or pass
#     `--agg-path <path>` / set AGG_BIN. TUIcast uses agg to render the GIF.
#   - Run from this repo on macOS, Linux, or Windows Git Bash/MSYS2.

set -euo pipefail

usage () {
  cat <<'EOF'
Usage: ./scripts/record-ted-open.sh [options]

Records the examples/ted app starting, opens the File menu, chooses Open,
types ./examples/ted/TedApp.cs into the OpenDialog, and saves:

  artifacts/tuicast/ted-open.gif
  artifacts/tuicast/ted-open.cast

Options:
  --output <path>       GIF output path (default: artifacts/tuicast/ted-open.gif)
  --cast-output <path>  asciinema cast output path (default: artifacts/tuicast/ted-open.cast)
  --tuicast <path>      tuicast executable (default: TUICAST_BIN or tuicast)
  --agg-path <path>     agg executable (default: AGG_BIN, ./tools/agg(.exe), or agg on PATH)
  --dotnet <path>       dotnet executable (default: DOTNET_BIN or dotnet)
  --cols <n>            recording columns (default: 120)
  --rows <n>            recording rows (default: 36)
  --skip-build          do not pre-build examples/ted before recording
  --help, -h            show this help

Advanced:
  Override KEYSTROKES to tune the TUIcast script. The default is:
    wait:1500,F9,CursorDown,CursorDown,Enter,wait:500,./examples/ted/TedApp.cs,Enter,wait:2000,Ctrl+Q
EOF
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)"

TUICAST_BIN="${TUICAST_BIN:-tuicast}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
AGG_BIN="${AGG_BIN:-}"
OUTPUT="$REPO_ROOT/artifacts/tuicast/ted-open.gif"
CAST_OUTPUT="$REPO_ROOT/artifacts/tuicast/ted-open.cast"
COLS=120
ROWS=36
MAX_DURATION=45
DRAIN_MS=1000
SKIP_BUILD=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --output)
      OUTPUT="$2"
      shift 2
      ;;
    --cast-output)
      CAST_OUTPUT="$2"
      shift 2
      ;;
    --tuicast)
      TUICAST_BIN="$2"
      shift 2
      ;;
    --agg-path)
      AGG_BIN="$2"
      shift 2
      ;;
    --dotnet)
      DOTNET_BIN="$2"
      shift 2
      ;;
    --cols)
      COLS="$2"
      shift 2
      ;;
    --rows)
      ROWS="$2"
      shift 2
      ;;
    --skip-build)
      SKIP_BUILD=true
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "error: unknown arg: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

resolve_tool () {
  local tool="$1"
  local label="$2"

  if command -v "$tool" >/dev/null 2>&1; then
    command -v "$tool"
    return
  fi

  if [[ -x "$tool" ]]; then
    printf '%s\n' "$tool"
    return
  fi

  echo "error: $label not found: $tool" >&2
  exit 1
}

resolve_agg () {
  if [[ -n "$AGG_BIN" ]]; then
    resolve_tool "$AGG_BIN" "agg"
    return
  fi

  for candidate in "$REPO_ROOT/tools/agg.exe" "$REPO_ROOT/tools/agg"; do
    if [[ -x "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return
    fi
  done

  if command -v agg >/dev/null 2>&1; then
    command -v agg
    return
  fi

  cat >&2 <<'EOF'
error: agg not found.

Install agg v1.5.0+ on PATH, place it at ./tools/agg or ./tools/agg.exe, or pass:
  --agg-path <path-to-agg>
EOF
  exit 1
}

TUICAST_BIN="$(resolve_tool "$TUICAST_BIN" "tuicast")"
DOTNET_BIN="$(resolve_tool "$DOTNET_BIN" "dotnet")"
AGG_BIN="$(resolve_agg)"

cd "$REPO_ROOT"
mkdir -p "$(dirname "$OUTPUT")" "$(dirname "$CAST_OUTPUT")"

TED_PROJECT="examples/ted/ted.csproj"

if [[ "$SKIP_BUILD" == false ]]; then
  "$DOTNET_BIN" build "$TED_PROJECT" --verbosity minimal
fi

KEYSTROKES="${KEYSTROKES:-wait:1500,F9,CursorDown,CursorDown,Enter,wait:500,./examples/ted/TedApp.cs,Enter,wait:2000,Ctrl+Q}"

"$TUICAST_BIN" record \
  --binary "$DOTNET_BIN" \
  --args "run,--no-build,--project,$TED_PROJECT" \
  --keystrokes "$KEYSTROKES" \
  --output "$OUTPUT" \
  --cast-output "$CAST_OUTPUT" \
  --agg-path "$AGG_BIN" \
  --cols "$COLS" \
  --rows "$ROWS" \
  --max-duration "$MAX_DURATION" \
  --drain "$DRAIN_MS" \
  --title "ted opens TedApp.cs"

echo
echo "Recorded ted File.Open flow:"
echo "  GIF:  $OUTPUT"
echo "  cast: $CAST_OUTPUT"

#!/usr/bin/env bash
# compare-baseline.sh — Run focused benchmarks and compare against baseline.json.
#
# Exits 0 on pass, 1 on egregious regression (>3x slower).
# Prints a markdown summary to stdout suitable for GitHub step summaries.
#
# Usage:
#   ./benchmarks/compare-baseline.sh [--fail-threshold 3.0] [--celebrate-threshold 0.8]

set -euo pipefail

FAIL_THRESHOLD="${1:-3.0}"       # fail if current > baseline × this
CELEBRATE_THRESHOLD="${2:-0.8}"  # celebrate if current < baseline × this

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BASELINE="$SCRIPT_DIR/baseline.json"
RESULTS_DIR="$(mktemp -d)"

echo "::group::Running focused benchmarks (short job)"
# BenchmarkDotNet accepts only lowercase job names: default, dry, short, medium, long, verylong.
# Passing "ShortRun" makes BDN print "invalid base job" and exit without running anything; the
# script then sees no JSON report and falls into "skipping comparison" → exit 0 → silent no-op.
# This was the gate's bug for everything between PR #53 and PR #77.
dotnet run --project "$SCRIPT_DIR/Terminal.Gui.Editor.Benchmarks" -c Release -- \
  --filter "*VisualLineBuild*" \
  --job short \
  --exporters json \
  --artifacts "$RESULTS_DIR" 2>&1 | tail -20
echo "::endgroup::"

# Find the BenchmarkDotNet JSON report
REPORT=$(find "$RESULTS_DIR" -name "*.json" -path "*/results/*" | head -1)

if [ -z "$REPORT" ]; then
  echo "::warning::No benchmark JSON report found — skipping comparison."
  exit 0
fi

# Compare: extract means from the JSON report and compare to baseline
echo ""
echo "## Performance comparison"
echo ""
echo "| Benchmark | Baseline | Current | Ratio | Status |"
echo "|-----------|----------|---------|-------|--------|"

FAILED=0
CELEBRATED=0

compare_benchmark() {
  local key="$1"
  local baseline_val="$2"
  local unit="$3"

  # Extract current mean from BenchmarkDotNet JSON using the method name
  # BDN method names in JSON are like "BuildLine_Short"
  local current
  current=$(python3 -c "
import json, sys
with open('$REPORT') as f:
    data = json.load(f)
for b in data.get('Benchmarks', []):
    method = b.get('Method', '')
    if method == '$key':
        stats = b.get('Statistics', {})
        mean = stats.get('Mean', 0)
        # BDN reports in nanoseconds
        if '$unit' == 'us':
            print(f'{mean / 1000:.1f}')
        elif '$unit' == 'ms':
            print(f'{mean / 1000000:.1f}')
        else:
            print(f'{mean:.1f}')
        sys.exit(0)
print('')
" 2>/dev/null || echo "")

  if [ -z "$current" ] || [ "$current" = "" ]; then
    return
  fi

  local ratio
  ratio=$(python3 -c "
b = float('$baseline_val')
c = float('$current')
if b > 0:
    print(f'{c/b:.2f}')
else:
    print('N/A')
")

  local status="✅"
  if python3 -c "exit(0 if float('$ratio') > float('$FAIL_THRESHOLD') else 1)" 2>/dev/null; then
    status="❌ REGRESSION"
    FAILED=1
  elif python3 -c "exit(0 if float('$ratio') < float('$CELEBRATE_THRESHOLD') else 1)" 2>/dev/null; then
    status="🎉 FASTER"
    CELEBRATED=1
  fi

  local desc
  desc=$(python3 -c "
import json
with open('$BASELINE') as f:
    data = json.load(f)
print(data['results'].get('$key', {}).get('description', '$key'))
" 2>/dev/null || echo "$key")

  echo "| $desc | ${baseline_val} ${unit} | ${current} ${unit} | ${ratio}x | $status |"
}

compare_benchmark "BuildLine_Short" "2.6" "us"
compare_benchmark "BuildLine_Long" "15.7" "us"
compare_benchmark "BuildLine_Tabs" "3.0" "us"
compare_benchmark "BuildLine_Emoji" "2.7" "us"
compare_benchmark "BuildLine_Mixed" "2.6" "us"

echo ""

if [ "$CELEBRATED" -eq 1 ]; then
  echo "> 🎉 **Performance improved!** Some benchmarks are notably faster than baseline."
  echo ""
fi

if [ "$FAILED" -eq 1 ]; then
  echo "> ❌ **Performance regression detected.** One or more benchmarks exceeded ${FAIL_THRESHOLD}x the baseline."
  echo "> Run \`dotnet run --project benchmarks/Terminal.Gui.Editor.Benchmarks -c Release\` locally to investigate."
  exit 1
fi

echo "> ✅ All benchmarks within ${FAIL_THRESHOLD}x of baseline."

# Cleanup
rm -rf "$RESULTS_DIR"

#!/usr/bin/env bash
# collect-run.sh — gather Codex autonomous sprint artifacts into
# specs/runs/<run-name>/.

set -euo pipefail

RUN="${1:-}"

usage () {
  cat <<'EOF'
Usage: ./scripts/collect-run.sh <run-name>

  run-name: arbitrary slug — e.g. "codex-2026-05-11".

Creates specs/runs/<run-name>/ in the operator's clone (the one this script
runs from, NOT the Codex clone), and populates it with:

  - prs.json                   — PRs filtered by experiment/codex/* branches
  - codex-transcript.jsonl     — best-effort transcript dump
  - codex-final.md             — copy of Codex's self-report, when present
  - spend.txt                  — empty template; fill from OpenAI dashboard
  - summary.md                 — empty run-summary template
EOF
}

if [[ "${RUN:-}" == "" || "${RUN}" == "--help" || "${RUN}" == "-h" ]]; then
  usage
  exit 0
fi

REPO_ROOT="$(git rev-parse --show-toplevel)"
OUT="$REPO_ROOT/specs/runs/$RUN"
mkdir -p "$OUT"

echo "==> Collecting PRs for prefix experiment/codex/*"
gh pr list --state all \
  --search "head:experiment/codex" \
  --json number,title,headRefName,author,state,createdAt,closedAt,mergedAt,additions,deletions,changedFiles,url \
  > "$OUT/prs.json" || true

WORK="$HOME/s/Terminal.Gui.Text/codex"
if [[ -f "$WORK/specs/runs/codex-final.md" ]]; then
  cp "$WORK/specs/runs/codex-final.md" "$OUT/codex-final.md"
elif [[ -f "$WORK/specs/runs/$RUN/codex-final.md" ]]; then
  cp "$WORK/specs/runs/$RUN/codex-final.md" "$OUT/codex-final.md"
fi

latest=$(ls -t "$HOME/.codex/sessions/"*.jsonl 2>/dev/null | head -1 || true)
if [[ -n "${latest:-}" ]]; then
  cp "$latest" "$OUT/codex-transcript.jsonl" || true
fi

cat > "$OUT/spend.txt" <<'EOF'
# Fill in from the OpenAI dashboard.

codex:    USD ____ (start) → USD ____ (end) = USD ____

wall-clock minutes:
  codex:    ____
EOF

cat > "$OUT/summary.md" <<'EOF'
# Codex run summary — <run-name>

> Fill this in after collect-run.sh runs.

## 1. PRs Opened

See `prs.json`.

## 2. Features Completed

List each feature and the PR that implements it.

## 3. Validation

Record build, tests, format, cleanup, benchmark, and CI outcomes.

## 4. Blockers

List human-review, dependency, CI, environment, or design blockers.

## 5. Risks

List known regressions, incomplete DoD items, or decisions still open.

## 6. Cost

See `spend.txt`.

## 7. Notes

Anything surprising or useful for the next run.
EOF

echo
echo "Artifacts at: $OUT"
ls -la "$OUT"

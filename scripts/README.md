# scripts/

Operator helpers for the Codex-only autonomous sprint described in `specs/codex-autonomous-sprint.md`.

All scripts are bash, target macOS (the Mac mini host), and are idempotent where practical. Each script supports `--help`.

## Run Order

```sh
# Day -1, on the Mac mini, ONCE per host:
./scripts/setup-host.sh

# Day -1, creates $HOME/s/Terminal.Gui.Editor/codex:
./scripts/setup-agent-clone.sh codex

# Optional: create one tracking issue for the Codex run:
./scripts/create-codex-run-issue.sh

# Day 0:
./scripts/start-experiment.sh
tmux attach -t codex-autonomy

# Day N, when Codex stops or you cut it off:
./scripts/collect-run.sh codex-2026-05-11
```

The artifact lands in `specs/runs/<run-name>/` — transcript, PR list, spend snapshot, copied final report when present, and a stub `summary.md`.

Codex integrates completed work into `experiment/codex/develop`. Use that branch for final checks before deciding whether anything should land on `develop`.

## What each script assumes

- `bash 4+` and `gh` on `$PATH`.
- The operator (you) is logged into `gh` with admin on `gui-cs/Text` so the issue creator and label-creator can run.
- `codex` is installed and logged in. `setup-host.sh` installs it, but `codex login` is interactive and stays manual.
- `gh auth status` in `$HOME/s/Terminal.Gui.Editor/codex` shows an identity that can push branches and open PRs.

## What each script will NOT do

- Create accounts.
- Spend money on your behalf without you running the script first.
- Modify `develop` directly.
- Merge PRs.

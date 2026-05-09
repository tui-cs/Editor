# scripts/

Operator helpers for the three-agent autonomous experiment described in `specs/10-autonomous-three-agent.md`.

All scripts are bash, target macOS (the Mac mini host), and are idempotent — re-running them is safe. Each script supports `--help`.

## Run order

### Test run (issue #37 — tabs)

```sh
# Day -1, on the Mac mini, ONCE per host:
./scripts/setup-host.sh

# Day -1, ONCE per agent (creates $HOME/s/Terminal.Gui.Text/{claude,codex,copilot} clones):
./scripts/setup-agent-clone.sh claude
./scripts/setup-agent-clone.sh codex
./scripts/setup-agent-clone.sh copilot

# Day -1, from any clone (creates the 3 mirrored issues on github.com):
./scripts/create-test-run-issues.sh

# Day 0 — one-liner:
./scripts/start-experiment.sh
tmux attach -t autonomy
# Then go to https://github.com/gui-cs/Text/issues/44 and assign Copilot.

# Day N — when you've seen enough, capture the artifact:
./scripts/collect-run.sh test-2026-05-09
```

The artifact lands in `specs/runs/test-2026-05-09/` — transcripts, PR list, spend snapshot, and a stub `comparison.md` for you to fill in.

### Full run

Same scripts, swap `create-test-run-issues.sh` for the full A1..E1 set (TODO: `create-full-run-issues.sh` not yet written — wait for the test run to land first).

## What each script assumes

- `bash 4+` and `gh` on `$PATH`.
- The operator (you) is logged into `gh` with admin on `gui-cs/Text` so the issue creator and label-creator can run.
- Per-agent gh auth uses three machine users / PATs — `setup-agent-clone.sh` checks for that posture but does not provision them. See spec §3.
- `claude` and `codex` CLIs are installed and logged in (per their own `--login` flow). `setup-host.sh` installs them but the login step is interactive and stays manual.

## What each script will NOT do

- Create accounts.
- Spend money on your behalf without you running the script first.
- Modify `develop` directly.
- Push branches under another agent's identity (the gh-auth posture in §3 prevents this).

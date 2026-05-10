#!/usr/bin/env pwsh
# create-test-run-issues.ps1 — create the three D1/tabs issues for the test run.

param([switch]$DryRun)

$repo = "gui-cs/Text"

function Ensure-Label($name, $color, $desc) {
    $existing = (gh -R $repo label list --json name | ConvertFrom-Json).name
    if ($name -notin $existing) {
        if ($DryRun) { Write-Host "DRY: gh label create $name" }
        else { gh -R $repo label create $name --color $color --description $desc }
    } else {
        Write-Host "  Label '$name' already exists"
    }
}

Write-Host "==> Ensuring labels exist on $repo"
Ensure-Label "agent:claude"  "5319e7" "Three-agent autonomy experiment — Claude Code"
Ensure-Label "agent:codex"   "1d76db" "Three-agent autonomy experiment — OpenAI Codex"
Ensure-Label "agent:copilot" "0e8a16" "Three-agent autonomy experiment — GitHub Copilot Coding Agent"
Ensure-Label "experiment"    "fbca04" "Tracking work created by the autonomy experiment"

$body = @"
## Test-run scope

You are one of three AI coding agents working in parallel on the same task. The full experiment is described in [\`specs/10-autonomous-three-agent.md\`](https://github.com/gui-cs/Text/blob/develop/specs/10-autonomous-three-agent.md).

**Your task:** implement [#37](https://github.com/gui-cs/Text/issues/37) — proper tab handling for \`gui-cs/Text\`.

**Required reading before you start:**

- [\`specs/00-plan.md\`](https://github.com/gui-cs/Text/blob/develop/specs/00-plan.md) — especially §0 (target), §4 (R1–R10 architectural rules), §8 D1 (the work-item brief and its dependency on B1), §9 (Definition of Done).
- [\`CLAUDE.md\`](https://github.com/gui-cs/Text/blob/develop/CLAUDE.md) — coding standards (the \`field\` keyword, \`var\` policy, expression-bodied vs block-bodied, etc.).
- [\`#37\`](https://github.com/gui-cs/Text/issues/37) — the full spec for tab handling.

**How to work:**

1. Open exactly one PR against \`develop\`. Branch name: \`experiment/<your-agent>/d1-tabs\` (replace \`<your-agent>\`).
2. When you stop — finished, stuck, or out of budget — write \`specs/runs/test-<your-agent>-final.md\` summarizing: what you did, what you skipped, why, total tokens spent, and what you would do differently.
3. Stop only when the PR is open and CI is either green or you have decided you cannot make it green.

**Do not edit:** \`specs/00-plan.md\`, \`CLAUDE.md\`, \`.claude/\`, \`.config/\`, \`.github/\`, \`third_party/\`, or \`scripts/\`. If you think one of those needs to change, write the proposal into \`specs/runs/test-<your-agent>-final.md\` instead.

**Do not pre-decide the B1 dependency.** §8 D1 says D1 depends on B1 (the \`VisualLineBuilder\` pipeline) and that without B1 the implementation should be rejected. You can: (a) refuse to ship until B1 lands, (b) implement B1 first and then D1, (c) ship a stopgap and explicitly own the R1/R2 violation in your PR description, or (d) ship a stopgap and pretend it's fine. Pick one. Your choice is part of the experiment.

## Terminal.Gui enlistment and bug-filing bar

A full clone of Terminal.Gui is at \`../Terminal.Gui\` (absolute path: \`~/s/Terminal.Gui.Text/Terminal.Gui\`, \`develop\` branch). Before using it, verify the clone is complete: \`git -C ../Terminal.Gui status\` should succeed and show a clean working tree.

When you encounter behavior you suspect is a Terminal.Gui bug:

1. **Reproduce it in a unit test that fails.** No failing test, no issue. The test goes in your PR's test project (or a new one), not in Terminal.Gui's tree.
2. **Verify the test fails for the right reason.** A failing test that fails because of *your* mistake is not a TG bug.
3. **Only then** file an issue on \`gui-cs/Terminal.Gui\`. Include the failing test code, the version of Terminal.Gui pinned in \`Directory.Build.props\`, the exact symptom, and a minimal repro.

The bar is high. If you cannot write a failing test that proves the bug, work around it locally, note the workaround in your final report, and move on.
"@

function Create-Issue($agent, $label) {
    $title = "[D1] Implement tab handling per #37 — agent: $agent"
    Write-Host "==> Creating: $title"
    if ($DryRun) {
        Write-Host "DRY: gh issue create --title '$title' --label $label --label experiment"
    } else {
        gh -R $repo issue create --title $title --body $body --label $label --label "experiment"
    }
}

Create-Issue "claude"  "agent:claude"
Create-Issue "codex"   "agent:codex"
Create-Issue "copilot" "agent:copilot"

Write-Host ""
Write-Host "Done. Next: ./scripts/start-agent.sh claude (and codex). Assign the agent:copilot issue to Copilot via the github.com UI."

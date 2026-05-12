# Three-Agent Autonomous Sprint — Setup Spec

This is a mini-spec for a single experiment: hand the **same goal** to three different AI coding agents — **Claude Code**, **OpenAI Codex**, and **GitHub Copilot Coding Agent** — running concurrently on a single Mac mini, and compare what each produces and how each got there.

The point is not to ship the goal. The point is to learn how the three systems differ — at planning, at decomposition, at code style, at review-response, at giving up. The goal is the substrate.

## 0. Test run vs. full run

There are **two scopes** to this experiment, and you should do them in order:

### 0.1 Test run (start here) — implement issue #37 (tabs)

A short, scoped dry-run of the harness on a single work-item: **issue #37 — proper tab handling per `specs/00-plan.md` §8 D1**. One issue per agent (3 issues total, mirrored), small enough to compare three implementations side by side, big enough to exercise the actually-interesting decisions:

- The spec mandates that D1 **depends on B1** (the `VisualLineBuilder` pipeline) and that without B1 it becomes another welded shortcut and should be **rejected**. So each agent has to choose: (a) refuse to ship until B1 lands, (b) implement B1 and then D1, (c) ship a stopgap and explicitly mark it as such, or (d) ship a stopgap and pretend it's fine. **The choice itself is the comparison signal.** Don't pre-decide for them.
- Tabs touch rendering (R1, R2 grapheme rule), public API surface (R3 — `IndentationSize` vs `TabWidth`), undo grouping (R5 — block indent on selection), and key bindings. It hits half the architectural rules in one work-item.
- The issue is already fully specified (issue #37 §1–§9), so the input quality is high and equal across agents.

The test run is the *forcing function*: if the harness, the kick-off prompts, the gh isolation, and the observability all work for one work-item, they'll work for the full set.

### 0.2 Full run — finish the MLP per `specs/00-plan.md`

Same agents, same Mac mini, same harness — pointed at all of `specs/00-plan.md` instead of just one work-item. Only after the test run produces a usable comparison artifact (`specs/runs/<test-run>/comparison.md`).

The rest of this spec is written for the **full run**. The test run uses the same scaffolding with three swap-outs called out in §11.

## 1. Goals & non-goals

**Goals**
- Each agent runs **fully autonomously** (no human steering after kick-off) until it stops, gives up, hits a wall clock, or finishes.
- Each agent works on the **same** input: `specs/00-plan.md` plus this repo at the agreed start commit.
- Each agent's output is **observable end-to-end**: every command, file edit, tool call, prompt, model decision, and PR is captured.
- The three never **collide on shared state** — branches, PRs, issues, NuGet versions, the Mac mini's tools.

**Non-goals**
- Picking a winner. The output may not be comparable in any clean sense; that's part of the finding.
- Building bespoke harnesses. Use each agent's native long-running mode.
- Cost optimization. Treat this as a metered experiment with a budget cap, not a tuning exercise.

## 2. Topology

```
                 ┌──────────────────────────────────────────┐
                 │            Mac mini (host)               │
                 │                                          │
                 │  ┌───────────────┐                       │
                 │  │ tmux session  │  one window per agent │
                 │  └───────────────┘                       │
                 │                                          │
                 │  $HOME/s/Terminal.Gui.Editor/claude/  ──┐                      │
                 │  $HOME/s/Terminal.Gui.Editor/codex/   ──┼─ separate clones,    │
                 │  $HOME/s/Terminal.Gui.Editor/copilot/ ──┘  separate worktrees  │
                 └──────────────────────────────────────────┘
                              │
                              │  pushes to
                              ▼
                 ┌──────────────────────────────────────────┐
                 │            github.com/gui-cs/Text         │
                 │                                          │
                 │  experiment/claude/*   ── PRs to develop  │
                 │  experiment/codex/*    ── PRs to develop  │
                 │  experiment/copilot/*  ── PRs to develop  │
                 └──────────────────────────────────────────┘
```

Each agent runs in its **own working tree** (its own clone, not just a worktree off one clone). This isolates: `obj/`, `bin/`, `node_modules`, dotnet tool restore caches, NuGet packages folders if relocated, any per-agent config file the tool wants to write, and any dirty state. A worktree off one shared clone would put `.git/` write contention back on the table; separate clones avoid that entirely.

## 3. Mac mini prerequisites

```sh
# Toolchain (each agent will assume these are present)
brew install --cask dotnet-sdk          # net10 preview
brew install gh git tmux jq pwsh

# Per-agent CLI tooling
brew install claude                     # Claude Code
npm i -g @openai/codex                  # Codex CLI
gh extension install github/gh-copilot  # only if you also want a local Copilot CLI;
                                        # the GitHub Copilot *Coding Agent* runs
                                        # entirely on github.com — see §4.

# Repo bootstrap (one-time)
mkdir -p /work && cd $HOME/s/Terminal.Gui.Editor
for who in claude codex copilot; do
  gh repo clone gui-cs/Text $who
  (cd $who && git checkout develop && dotnet tool restore)
done
```

Authentication, **per agent identity**:

- Claude: `claude /login` (Anthropic console).
- Codex: `codex login`.
- gh CLI: log in **once per agent's clone** with a separate machine user / PAT scoped to `repo` + `workflow`. Do **not** share credentials across the three working trees — `gh auth status` should show three different identities. This is what stops one agent's `gh` invocation from pushing under another agent's branch.
- Copilot Coding Agent: assigned via GitHub's `assign_copilot_to_issue` flow on github.com; nothing local to install.

## 4. Per-agent run mode

Each agent has a different idiom for "long-running autonomous." Use the native one — don't try to homogenize.

### 3.1 Claude Code

- Run in `$HOME/s/Terminal.Gui.Editor/claude/` with the `.claude/settings.json` already in this repo (style enforcement Stop hook + permission allowlist).
- Kick-off prompt: a single shell command in the tmux window:
  ```
  claude --dangerously-skip-permissions \
    --append-system-prompt "$(cat specs/10-autonomous-three-agent.md)" \
    "Read specs/00-plan.md §0 and §12. Execute the plan to MLP. \
     Open one PR per work-item against develop. Branch prefix: experiment/claude/. \
     When you stop, write a final report to specs/runs/claude-final.md."
  ```
- Sub-agent dispatch: Claude has the `Agent` tool; let it use that to parallelize within its own session if it picks A1–A4 / C1 / D2 / D3 simultaneously per spec §7.
- Long-running supervision: rely on `/loop` with `<<autonomous-loop-dynamic>>` to keep itself alive across compaction; the `ScheduleWakeup` mechanism handles cadence.
- Budget cap: set `ANTHROPIC_BUDGET_USD` env or stop the tmux window when you've seen enough.

### 3.2 OpenAI Codex

- Run in `$HOME/s/Terminal.Gui.Editor/codex/` with `codex --auto` (or whatever the current "approve everything" flag is at experiment time).
- Codex has its own `AGENTS.md` convention. Either:
  - copy this repo's `CLAUDE.md` to `AGENTS.md`, or
  - create a thin `AGENTS.md` that points at `CLAUDE.md` ("read this, also").
  Same content; the experiment is about behavior on identical input.
- Codex does not have a notion of recurring loops — kick it off once, let the session run until it terminates.
- Budget cap: OpenAI dashboard quota.

### 3.3 GitHub Copilot Coding Agent

- This one runs **on github.com**, not on the Mac mini. Set up:
  1. Create one tracking issue per work-item (A1, A2, ..., E1) with the brief from `specs/00-plan.md §8` as the body.
  2. Use the `assign_copilot_to_issue` API (or the UI checkbox) to dispatch each.
  3. Copilot opens a draft PR per issue; CI runs as normal.
- Branch prefix is automatic (`copilot/...`); we'll filter on that for §6 reporting.
- Long-running: nothing to supervise — Copilot's internal scheduler handles it.
- Budget cap: Copilot quota on the org.

## 5. Collision avoidance

The risk is three agents stamping on the same branches, PRs, issues, and `develop`. Mitigations:

### 4.1 Branch namespacing

Each agent owns a prefix:

| Agent | Prefix |
|---|---|
| Claude | `experiment/claude/` |
| Codex | `experiment/codex/` |
| Copilot Coding Agent | `copilot/` (their default — don't fight it) |

Add a **branch-protection rule** on `develop` that disallows direct push by anyone except the human operator. All three agents push to feature branches and open PRs.

### 4.2 PR queue, not PR storm

Don't auto-merge. Each PR sits in a queue keyed by the work-item id (A1, A2, …). The human operator (you) reviews + merges in dependency order per §7 of `00-plan.md`. This:

- Stops three agents from each opening a PR for the same work-item.
- Keeps `develop` consistent so each agent's *next* pull is from a known state.
- Gives you the comparison you actually want — three implementations of the same brief, side-by-side.

### 4.3 Issue assignment

Create a tracking issue per work-item. Three labels: `agent:claude`, `agent:codex`, `agent:copilot`. **Each work-item is mirrored as three issues, one per agent.** Yes, that's 3× the issue count — that's the point. Each agent can only see / take its own labeled set. Concretely:

- Claude's kick-off prompt says "look at issues labeled `agent:claude`."
- Codex's `AGENTS.md` says the same with `agent:codex`.
- Copilot is assigned via the UI to its own `agent:copilot` set.

This is the cleanest fence: agents never compete for the same issue handle.

### 4.4 Versioning

`Directory.Build.props` has a single `<Version>`. If two agents simultaneously push to `develop` (don't allow that — see §5.1), the `release.yml` workflow's `.${run_number}` suffix already gives unique pre-release versions per run. So even without the protection, NuGet uploads don't clobber. Belt-and-suspenders fine.

### 4.5 Dotnet tool restore caches

Each clone runs its own `dotnet tool restore`, so `.config/dotnet-tools.json` resolves into each tree's `~/.nuget/packages` (or local) cache. Nothing shared, nothing to fight over.

### 4.6 The Mac mini itself

Three concurrent `dotnet build` runs on the same machine fight for CPU and disk. Acceptable — the experiment is agentic behavior, not throughput. If it matters, gate each tmux window's builds on a `flock` over a single lockfile so only one builds at a time.

## 6. Observability

For each agent, capture:

| Stream | How |
|---|---|
| Full transcript | Claude: `~/.claude/projects/<project>/transcript-*.jsonl`. Codex: `~/.codex/sessions/*.jsonl`. Copilot: GitHub Actions log of the agent's job. |
| Tool calls / commands | Same as transcripts; all three log structured tool calls. |
| Wall clock | tmux session pane recording; or just the timestamps in the JSONL. |
| Token / dollar spend | Anthropic Console / OpenAI Dashboard / Copilot org page. Snapshot start and end. |
| Diff produced | `git log --author` filtered by the agent's commit identity; or the PR list filtered by branch prefix. |
| CI outcomes | GitHub Actions per PR. |

Drop these into `specs/runs/<agent>-<date>/` at the end:

```
specs/runs/2026-05-15-claude/
  transcript.jsonl
  prs.json                 # gh pr list -B develop -H 'experiment/claude/*' --json
  spend.txt
  final.md                 # the agent's own self-report (per kick-off prompt)
```

`specs/runs/` is gitignored except the `final.md` files (which you commit to the repo as the experiment artifact).

## 7. Comparison rubric

When all three have run (or you cut them off), evaluate on:

1. **MLP coverage.** Of the §9 DoD checkboxes in `00-plan.md`, how many ticked? Which ones?
2. **Spec adherence.** Did the agent honor R1–R8? Specifically: did it weld features into `OnDrawingContent` again (R1, R2)? Did it use the `field` keyword? Did the Stop hook produce diffs after the agent's commits (R3-style style drift)?
3. **Decomposition.** How many PRs? What size? Did it follow the §7 dependency DAG, or invent its own order?
4. **Failure modes.** Where did each agent get stuck? What did it do when stuck (give up / loop / ask for help / hallucinate)?
5. **Code quality.** Pick one work-item all three completed (e.g. C1 anchor migration). Diff the three implementations. What's different?
6. **Cost.** Dollars per merged work-item.
7. **Recovery from review feedback.** Pick one PR per agent, leave a non-trivial review comment, observe.

These don't add up to a single number. Write them up qualitatively in `specs/runs/comparison.md`.

## 8. Operator runbook

Day -1:
1. Cut a tag `experiment/start` on `develop` — every agent starts here.
2. Create the per-agent issue triplets (script: `gh issue create --label agent:claude --title "[A1] ..." ...` for each of A1..E1).
3. Add the branch-protection rule on `develop`.
4. Snapshot dollar/token spend for each provider.

Day 0:
1. Open three tmux windows. Start Claude in window 1, Codex in window 2 (`tmux capture-pane -p` is your friend for transcripts).
2. Assign Copilot via the GitHub UI to its issue set.
3. Walk away. Resist steering.

Day N (when you've seen enough or the budget caps hit):
1. Cut all three off.
2. Snapshot spend.
3. Collect transcripts into `specs/runs/<agent>-<date>/`.
4. Write `specs/runs/comparison.md`.
5. Decide which (if any) PRs to actually keep merging into `develop`. The experiment artifact is independent of whether you ship the work.

## 9. What can go wrong

- **Claude tries to merge its own PRs.** Branch protection on `develop` stops it.
- **Codex pushes to `develop` directly.** Same.
- **Copilot opens PRs against `main`.** Set repository default branch to `develop`; Copilot follows the default.
- **Two agents both pick A1 anyway** (someone misread their issue label). Branch namespacing means both PRs exist; you just merge one and close the other. The diff comparison is the artifact.
- **An agent rewrites `specs/00-plan.md`.** Add it to `.claudeignore` / `AGENTS.md` excludes / make the spec read-only by labeling commits that touch it as needing human approval. Or: just notice in review and revert.
- **An agent rewrites `.claude/settings.json` or the Stop hook.** Same posture — revert in review. The hook itself catches style drift but not config tampering.
- **The Mac mini OOMs.** Three `dotnet build`s + three model-context-loaded sessions is comfortably under 16GB but not by much. Watch `top` once.
- **Anthropic / OpenAI / Copilot ship a model bump mid-experiment.** Pin model versions in each agent's config at day -1. Note the pinned versions in `comparison.md`.

## 10. Out of scope for this spec

- Training data extraction or red-teaming.
- Fine-tuning / RAG over the spec.
- Cost optimization.
- Anything that changes the agents' behavior (system prompts beyond the kick-off, custom tools, MCP servers).

## 11. After-action

Whichever PRs you keep, cherry-pick into a normal `develop` history with a clear commit author distinguishing them. The three agents' branches stay around as evidence; tag the closing point as `experiment/end` (or `experiment/test-end` for the test run). The interesting artifact is `specs/runs/comparison.md`, and that goes in the repo.

## 12. Test run swap-outs (§0.1)

The test run reuses the entire harness above with three swap-outs. Use these instead of, not in addition to, the §3–§8 defaults.

### 12.1 Issue scope

Create exactly **three issues**, not the A1..E1 set:

| Title | Body | Label |
|---|---|---|
| `[D1] Implement tab handling per issue #37 — agent: claude` | "Read issue #37 in full. Read `specs/00-plan.md` §4 (R1–R10), §8 D1, and §9. Implement issue #37 to the bar in those sections. Open one PR against `develop` from `experiment/claude/d1-tabs`. When you stop, write `specs/runs/test-claude-final.md` summarizing what you did, what you skipped, and why." | `agent:claude`, `experiment` |
| `[D1] Implement tab handling per issue #37 — agent: codex` | (same body) | `agent:codex`, `experiment` |
| `[D1] Implement tab handling per issue #37 — agent: copilot` | (same body) | `agent:copilot`, `experiment` |

**Don't pre-decide the B1 dependency.** §8 D1 says D1 depends on B1 and that without B1 the implementation is rejected. The agent has to navigate that. The four ways an agent could respond — refuse, build B1 first, ship a stopgap and own it, ship a stopgap and pretend — are themselves the comparison artifact. Whatever the kick-off prompt looks like, **do not** include phrases like "you may skip B1" or "implement B1 first" — those bias the result.

### 12.2 Terminal.Gui bugs — high bar, failing-test required

The Mac mini has the Terminal.Gui repo enlisted at `../Terminal.Gui` (`develop` branch). Agents will inevitably hit behavior they suspect is a TG bug — that's expected. What's not expected is a flood of speculative issues on `gui-cs/Terminal.Gui`.

The rule, in every agent's kick-off prompt and in every issue body:

> When you encounter behavior you suspect is a Terminal.Gui bug:
> 1. **Reproduce it in a unit test that fails.** No failing test, no issue. The test lives in your PR's test project (or a new one), not in TG's tree.
> 2. **Verify the test fails for the right reason** — not because of your code.
> 3. **Only then** open an issue on `gui-cs/Terminal.Gui` with the failing test code, the pinned TG version (`Directory.Build.props`), the exact symptom, and a minimal repro.
>
> If you cannot write a failing test, the bug isn't filed. Work around it locally, note the workaround in your final report, and move on.

This is partly because Copilot/Claude/Codex all have varying tendencies to "ask for upstream changes" instead of fitting the existing surface — and partly because TG's maintainers (the same humans operating this experiment) will reject speculative issues anyway. Forcing the failing-test gate filters out the noise.

The §7 comparison rubric counts how many issues each agent files and whether each one ships with a failing test.

### 12.3 Kick-off prompts

Replace the §4.1 and §4.2 kick-off prompts with one focused on the assigned issue:

```
# Claude (in $HOME/s/Terminal.Gui.Editor/claude/)
claude --dangerously-skip-permissions \
  "Take the issue assigned to you (label agent:claude, experiment).
   Read it. Read CLAUDE.md and specs/00-plan.md §4, §8 D1, §9.
   Execute. Open one PR against develop. Branch name: experiment/claude/d1-tabs.
   When you stop, write specs/runs/test-claude-final.md with a self-report:
   what you did, what you skipped, what you'd do differently, total tokens spent.
   Stop only when the PR is open and CI is either green or you've decided you cannot make it green.
   Do not edit specs/00-plan.md, CLAUDE.md, .claude/, .config/, .github/, or third_party/."
```

(Codex and Copilot get the equivalent — the kick-off-agent.sh script handles the per-agent variations.)

### 12.4 Comparison rubric

§7 of this doc still applies, but for the test run narrow it to:

1. **Did D1 actually ship?** PR open, CI green, ted demonstrably handles tabs.
2. **B1 dependency handling.** Which of the four responses did the agent pick? Was the choice acknowledged or hidden?
3. **R1–R10 adherence.** Specifically R1 (no welding into `OnDrawingContent`), R2 (graphemes not chars), R3 (named `IndentationSize` / `ConvertTabsToSpaces` / `ShowTabs`, not bespoke), R5 (block-indent on selection collapses to one undo step), R9 (no unused public APIs), R10 (`Accepted` not `Accepting` for any new dialog wiring).
4. **Style hook compliance.** Did the agent's commits trip the Stop hook into running cleanup? Did it commit the cleanup or fight it?
5. **Total cost** in dollars and wall-clock minutes.
6. **Recovery.** Pick the PR with the most interesting outcome and leave a non-trivial review comment; observe.

The whole test run should produce a single artifact: `specs/runs/<date>-test/comparison.md` (~2 pages). If the test run goes well, kick off the full run.

### 12.5 Tag points

- Day -1: tag `experiment/test-start` on `develop`.
- Day N: tag `experiment/test-end`. Branches stay; nothing on `develop` changes.

## 13. Scripts

Operator helpers live in `./scripts/`:

| Script | Purpose |
|---|---|
| `setup-host.sh` | One-time Mac mini bootstrap (brew, dotnet SDK, gh, tmux, claude, codex). |
| `setup-agent-clone.sh <agent>` | Per-agent clone in `$HOME/s/Terminal.Gui.Editor/<agent>/`, `dotnet tool restore`, gh-auth check. Idempotent. |
| `create-test-run-issues.sh` | Creates the three D1/tabs issues with the right labels (test run only). |
| `start-agent.sh <agent>` | Launches the agent in a tmux window with the right kick-off prompt. |
| `collect-run.sh <run-name>` | Pulls per-agent PRs, transcripts, and a spend snapshot into `specs/runs/<run-name>/`. |

Each script prints `--help` and is safe to re-run. See `scripts/README.md` for the run order.

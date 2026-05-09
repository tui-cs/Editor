# Three-Agent Autonomous MLP Sprint — Setup Spec

This is a mini-spec for a single experiment: hand the **same goal** ("finish the MLP per `specs/00-plan.md`") to three different AI coding agents — **Claude Code**, **OpenAI Codex**, and **GitHub Copilot Coding Agent** — running concurrently on a single Mac mini, and compare what each produces and how each got there.

The point is not to ship the MLP. The point is to learn how the three systems differ — at planning, at decomposition, at code style, at review-response, at giving up. The MLP is the substrate.

## 0. Goals & non-goals

**Goals**
- Each agent runs **fully autonomously** (no human steering after kick-off) until it stops, gives up, hits a wall clock, or finishes.
- Each agent works on the **same** input: `specs/00-plan.md` plus this repo at the agreed start commit.
- Each agent's output is **observable end-to-end**: every command, file edit, tool call, prompt, model decision, and PR is captured.
- The three never **collide on shared state** — branches, PRs, issues, NuGet versions, the Mac mini's tools.

**Non-goals**
- Picking a winner. The output may not be comparable in any clean sense; that's part of the finding.
- Building bespoke harnesses. Use each agent's native long-running mode.
- Cost optimization. Treat this as a metered experiment with a budget cap, not a tuning exercise.

## 1. Topology

```
                 ┌──────────────────────────────────────────┐
                 │            Mac mini (host)               │
                 │                                          │
                 │  ┌───────────────┐                       │
                 │  │ tmux session  │  one window per agent │
                 │  └───────────────┘                       │
                 │                                          │
                 │  /work/claude/  ──┐                      │
                 │  /work/codex/   ──┼─ separate clones,    │
                 │  /work/copilot/ ──┘  separate worktrees  │
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

## 2. Mac mini prerequisites

```sh
# Toolchain (each agent will assume these are present)
brew install --cask dotnet-sdk          # net10 preview
brew install gh git tmux jq pwsh

# Per-agent CLI tooling
brew install claude                     # Claude Code
npm i -g @openai/codex                  # Codex CLI
gh extension install github/gh-copilot  # only if you also want a local Copilot CLI;
                                        # the GitHub Copilot *Coding Agent* runs
                                        # entirely on github.com — see §3.

# Repo bootstrap (one-time)
mkdir -p /work && cd /work
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

## 3. Per-agent run mode

Each agent has a different idiom for "long-running autonomous." Use the native one — don't try to homogenize.

### 3.1 Claude Code

- Run in `/work/claude/` with the `.claude/settings.json` already in this repo (style enforcement Stop hook + permission allowlist).
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

- Run in `/work/codex/` with `codex --auto` (or whatever the current "approve everything" flag is at experiment time).
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

## 4. Collision avoidance

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

`Directory.Build.props` has a single `<Version>`. If two agents simultaneously push to `develop` (don't allow that — see §4.1), the `release.yml` workflow's `.${run_number}` suffix already gives unique pre-release versions per run. So even without the protection, NuGet uploads don't clobber. Belt-and-suspenders fine.

### 4.5 Dotnet tool restore caches

Each clone runs its own `dotnet tool restore`, so `.config/dotnet-tools.json` resolves into each tree's `~/.nuget/packages` (or local) cache. Nothing shared, nothing to fight over.

### 4.6 The Mac mini itself

Three concurrent `dotnet build` runs on the same machine fight for CPU and disk. Acceptable — the experiment is agentic behavior, not throughput. If it matters, gate each tmux window's builds on a `flock` over a single lockfile so only one builds at a time.

## 5. Observability

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

## 6. Comparison rubric

When all three have run (or you cut them off), evaluate on:

1. **MLP coverage.** Of the §9 DoD checkboxes in `00-plan.md`, how many ticked? Which ones?
2. **Spec adherence.** Did the agent honor R1–R8? Specifically: did it weld features into `OnDrawingContent` again (R1, R2)? Did it use the `field` keyword? Did the Stop hook produce diffs after the agent's commits (R3-style style drift)?
3. **Decomposition.** How many PRs? What size? Did it follow the §7 dependency DAG, or invent its own order?
4. **Failure modes.** Where did each agent get stuck? What did it do when stuck (give up / loop / ask for help / hallucinate)?
5. **Code quality.** Pick one work-item all three completed (e.g. C1 anchor migration). Diff the three implementations. What's different?
6. **Cost.** Dollars per merged work-item.
7. **Recovery from review feedback.** Pick one PR per agent, leave a non-trivial review comment, observe.

These don't add up to a single number. Write them up qualitatively in `specs/runs/comparison.md`.

## 7. Operator runbook

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

## 8. What can go wrong

- **Claude tries to merge its own PRs.** Branch protection on `develop` stops it.
- **Codex pushes to `develop` directly.** Same.
- **Copilot opens PRs against `main`.** Set repository default branch to `develop`; Copilot follows the default.
- **Two agents both pick A1 anyway** (someone misread their issue label). Branch namespacing means both PRs exist; you just merge one and close the other. The diff comparison is the artifact.
- **An agent rewrites `specs/00-plan.md`.** Add it to `.claudeignore` / `AGENTS.md` excludes / make the spec read-only by labeling commits that touch it as needing human approval. Or: just notice in review and revert.
- **An agent rewrites `.claude/settings.json` or the Stop hook.** Same posture — revert in review. The hook itself catches style drift but not config tampering.
- **The Mac mini OOMs.** Three `dotnet build`s + three model-context-loaded sessions is comfortably under 16GB but not by much. Watch `top` once.
- **Anthropic / OpenAI / Copilot ship a model bump mid-experiment.** Pin model versions in each agent's config at day -1. Note the pinned versions in `comparison.md`.

## 9. Out of scope for this spec

- Training data extraction or red-teaming.
- Fine-tuning / RAG over the spec.
- Cost optimization.
- Anything that changes the agents' behavior (system prompts beyond the kick-off, custom tools, MCP servers).

## 10. After-action

Whichever PRs you keep, cherry-pick into a normal `develop` history with a clear commit author distinguishing them. The three agents' branches stay around as evidence; tag the closing point as `experiment/end`. The interesting artifact is `specs/runs/comparison.md`, and that goes in the repo.

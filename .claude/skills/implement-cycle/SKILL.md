---
name: implement-cycle
description: >
  Execute the next incomplete TDD cycle from a GitHub tracking issue: Red → Green → Refactor → Verify → PR.
  Use this skill whenever the user wants to work through the next planned cycle, progress the implementation plan, run the next TDD iteration, or says "next cycle" — even if they don't specify which one.
  Triggers on phrases like "do the next cycle", "implement cycle X.Y", "implement the next cycle of #76", "work through phase 2", "continue the implementation plan", or "what's the next step in the plan".
---

Execute the next incomplete TDD cycle from a GitHub tracking issue: Red → Green → Refactor → Verify → PR.

## Input

Optional specifier — one of:
- `#<n>` or `<n>`: parent tracking issue number (e.g. `#76`). Finds the lowest-numbered open sub-issue whose title matches `[<n>.`.
- `#<n>-#<m>`: explicit parent + sub-issue (e.g. `#76-#86`). Targets that sub-issue directly.
- Omitted: searches all open issues for the lowest-numbered issue whose title matches `[*.` pattern.

## Steps

### 1. Find the cycle issue

```bash
gh issue list --repo kommundsen/Conjecture --state open --json number,title \
  | grep -i "\[<n>\."
```

Pick the lowest-numbered result. Extract:
- **Sub-issue number** (e.g. `86`)
- **Cycle number** from the title (e.g. `76.1`)
- **Short description** from the title after `]` — slugify to kebab-case (lowercase, spaces→hyphens, strip special chars)

Read the full issue body:
```bash
gh issue view <sub-issue-number> --repo kommundsen/Conjecture
```

Extract the **## Test** and **## Implement** sections from the body.

Print a summary line so the user knows what is being worked on:
```
Cycle <cycle-number>: <title>  (#<sub-issue-number>)
```

### 2. Create a branch

Branch name format: `feat/<parent>-<sub>-<slug>` (no `#` in the git branch name).

First, update main:
```bash
git checkout main && git pull
```

Then check whether gh-stack is available and whether a stack already exists for this parent:
```bash
gh stack view --json 2>/dev/null
```

- Command fails entirely → `STACK_MODE=unavailable`
- Output contains a branch matching `feat/<parent>-` → `STACK_MODE=add`
- Otherwise → `STACK_MODE=init`

**`STACK_MODE=unavailable`** — announce fallback, create branch manually:
```bash
git checkout -b feat/<parent>-<sub>-<slug>
```

**`STACK_MODE=init`** — initialize a new stack:
```bash
gh stack init feat/<parent>-<sub>-<slug>
```
(creates and checks out the branch — do NOT also run `git checkout -b`)

**`STACK_MODE=add`** — navigate to top of the existing stack, then add a new layer:
```bash
gh stack top
gh stack add feat/<parent>-<sub>-<slug>
```

In both stack modes (`init` and `add`), sync to pick up any merges from previous cycles:
```bash
gh stack sync
```
If `gh stack sync` reports conflicts, stop and ask the user to resolve them before continuing.

### 3–5. TDD loop (max 3 iterations)

Repeat the following loop. Track the iteration count; stop after 3 and report if never approved.

#### 3a. Red phase — write failing tests (test-developer agent)

On the **first iteration**: spawn a `test-developer` agent with:
- The full `## Test` section from the issue
- The test file path from the issue

On **subsequent iterations** (ADD_TEST verdict): spawn a `test-developer` agent with:
- The reviewer's findings from the previous iteration
- The existing test file path

After the agent returns, format only the files it changed:
```bash
git diff --name-only HEAD && git ls-files --others --exclude-standard src/
```
Collect all `.cs` paths from that output, then run:
```bash
dotnet format src/ --include "<file1>" --include "<file2>" ... --exclude-diagnostics IDE0130
```

Run `dotnet build src/` — must fail or have test failures (red). If unexpectedly green, stop and report.

#### 3b. Green phase — implement (developer agent)

Spawn a `developer` agent with the test class name extracted from the test file path.

After the agent returns, format only the files it changed:
```bash
git diff --name-only HEAD && git ls-files --others --exclude-standard src/
```
Collect all `.cs` paths from that output, then run:
```bash
dotnet format src/ --include "<file1>" --include "<file2>" ... --exclude-diagnostics IDE0130
```

Run `dotnet test src/ --filter "FullyQualifiedName~<TestClassName>"`.

If tests still fail, spawn the `developer` agent again with the failing output as additional context. If still failing after 2 total attempts, stop and report.

#### 3c. Review phase — assess quality (reviewer agent)

Spawn a `reviewer` agent with:
- The git diff of all production files changed since branch creation (`git diff main HEAD -- src/ ':!*.Tests*'`)
- The test results from 3b

#### 3d. User checkpoint

Present the reviewer's verdict and findings to the user using AskUserQuestion:

```
Reviewer verdict: <APPROVED | FIX_IMPLEMENTATION | ADD_TEST>

Findings:
<bullet list from reviewer>

What would you like to do?
```

Options:
- **Fix implementation** — re-run 3b with reviewer findings threaded as context (skip 3a)
- **Add / refine tests** — re-run from 3a with reviewer findings as context
- **Approve** — exit loop and proceed to PublicAPI check
- **Abort** — stop here, leave branch as-is

If the verdict is APPROVED, still show the checkpoint but default-highlight "Approve".

### 6. PublicAPI check

If the **## Implement** section mentions new public API surface, verify `PublicAPI.Unshipped.txt` was updated with the new symbols. If not, update it now.

### 7. Commit

Invoke the `commit-message` skill to generate a suggested commit message.

Stage all new and modified files from this cycle and commit with the suggested message (no `Co-Authored-By` trailer).

### 8. Push branch and create PR

**`STACK_MODE=unavailable`** — push and create a PR against `main` (same as before):

```bash
git push -u origin feat/<parent>-<sub>-<slug>
```

Read `.github/pull_request_template.md` and fill it in:

```bash
gh pr create \
  --repo kommundsen/Conjecture \
  --title "[<cycle>] <title>" \
  --base main \
  --body "$(cat <<'EOF'
<filled-in pull_request_template.md content>

Closes #<sub-issue-number>
Part of #<parent-issue-number>
EOF
)"
```

**`STACK_MODE=init` or `STACK_MODE=add`** — submit the stack:

Fill in `.github/pull_request_template.md` with this cycle's description plus:
```
Closes #<sub-issue-number>
Part of #<parent-issue-number>
```

Then run:
```bash
gh stack submit
```

When prompted for the PR title for this cycle's branch, enter: `[<cycle>] <title>`
When prompted for the PR body, paste the filled-in template. Do NOT use `--auto` — the title and `Closes #N` body are load-bearing for GitHub's sub-issue tracking.

For any previously-submitted PRs in the stack that are already open, accept the existing title as-is.

Print the PR URL for this cycle's branch:
```bash
gh pr view feat/<parent>-<sub>-<slug> --json url --jq '.url'
```

## Guidelines

- One cycle per invocation — do not cascade into the next cycle.
- If the issue references a `/decision` step, invoke the `decision` skill before the loop.
- Never create the PR if the build or tests are red.
- Scope all changes to what the cycle issue demands.
- When gh-stack is available: branch off the previous cycle via `gh stack add`, not off `main` directly. When unavailable: branch off `main`.
- Always call `gh stack top` before `gh stack add` — `add` must be run from the topmost branch.
- `gh stack sync` at Step 2 handles merges of previous cycles; do not manually rebase onto main.
- `STACK_MODE` is a local variable for this invocation — re-detect it fresh each run.

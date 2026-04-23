---
name: implement-enhancement
description: >
  Drive an entire GitHub enhancement issue to completion as one feature branch and one PR, looping through its sub-issues as TDD cycles (each committed individually).
  Use this skill when the user wants to ship a coherent multi-sub-issue enhancement as a single reviewable PR rather than one PR per sub-issue (which is `implement-cycle`'s behavior).
  Triggers on phrases like "implement enhancement #N", "work through all of #N", "drive #N to done on one branch", "one PR for the whole enhancement", or "implement #N end-to-end".
---

Drive an enhancement parent issue to completion: one feature branch, one commit per sub-issue TDD cycle, one draft PR that goes ready-for-review at the end.

For single-cycle / one-PR-per-sub-issue work, use `implement-cycle` instead.

## Input

- `#<parent>` or `<parent>` — parent enhancement issue number (required, e.g. `#449`).

## Context hygiene principle

The main thread holds **only**: parent issue #, branch name, contributor list, the cycle summaries returned by `cycle-runner` (one per cycle). Everything else — full issue bodies, comments, per-agent transcripts, diffs, review findings — lives inside subagents and git/GitHub state, not in the main transcript. Call `mcp__ccd_session__mark_chapter` before every cycle so compaction has natural boundaries.

## Steps

### 1. Resolve parent + sub-issues

```bash
gh issue view <parent> --repo kommundsen/Conjecture --json number,title,body,state,labels
gh issue list --repo kommundsen/Conjecture --state open --json number,title --limit 200 \
  | jq -r '.[] | select(.title | test("^\\[" + "<parent>" + "\\."))'
```

Extract:
- **Parent title** (for the PR title and branch slug)
- **Parent slug**: from parent title, slugify (kebab-case, strip special chars)
- **Sub-issue list**: every open issue whose title starts with `[<parent>.`, sorted by number ascending

Print a summary line:
```
Enhancement #<parent>: <title>  (<k> open sub-issues)
```

### 2. Fetch contributors (cache for the run)

```bash
CONTRIBUTORS=$(gh api repos/kommundsen/Conjecture/contributors --jq '.[].login' | tr '\n' ' ')
```

### 3. Whole-enhancement brief

Spawn the `issue-context` agent with: `issues = [<parent>, <sub1>, <sub2>, …]`, `contributors = $CONTRIBUTORS`.

If the returned brief's `Non-contributor comments to review` section is non-empty, use `AskUserQuestion` to ask the user whether any of those comments require attention before proceeding. Options: "Proceed" / "Pause to address them".

### 4. Branch (create or resume)

```bash
BRANCH="feat/#<parent>-<slug>"
if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
  git checkout "$BRANCH"                # resume mode
else
  git checkout main && git pull && git checkout -b "$BRANCH"
fi
```

Print whether this is a fresh start or a resume.

### 5. Mark parent In Progress on the Roadmap project

Reuse the `set_in_progress` helper from `implement-cycle/SKILL.md`:

```bash
set_in_progress() {
  local number=$1
  local url="https://github.com/kommundsen/Conjecture/issues/$number"
  gh project item-add 2 --owner kommundsen --url "$url" > /dev/null 2>&1 || true
  local item_id=$(gh project item-list 2 --owner kommundsen --format json --limit 200 \
    --jq ".items[] | select(.content.number == $number) | .id")
  gh project item-edit --project-id PVT_kwHOAAZ3vM4BTArq --id "$item_id" \
    --field-id PVTSSF_lAHOAAZ3vM4BTArqzhAYMcQ \
    --single-select-option-id 47fc9ee4
}

PARENT_STATUS=$(gh project item-list 2 --owner kommundsen --format json --limit 200 \
  --jq ".items[] | select(.content.number == <parent>) | .status")
if [ "$PARENT_STATUS" != "In Progress" ]; then
  set_in_progress <parent>
fi
```

### 6. Per-cycle loop

Re-query open sub-issues at the start of every iteration (idempotent; tolerates external closes and resume mode). Loop while at least one sub-issue remains.

For each iteration:

#### 6a. Chapter marker + In Progress

```text
mcp__ccd_session__mark_chapter title="Cycle <cycle>: <sub-title>"
```
Then `set_in_progress <sub-issue>`.

#### 6b. Per-cycle context brief

Spawn `issue-context` with `issues = [<sub-issue>]`, `contributors = $CONTRIBUTORS`. If it flags non-contributor comments, `AskUserQuestion` before continuing.

#### 6c. Run the cycle

Spawn `cycle-runner` with:
- `sub_issue_number`
- `cycle_number` (from the sub-issue title, e.g. `76.1`)
- `parent_issue_number`
- `brief` — the structured output from 6b
- `test_file_path` — from the sub-issue body
- `test_class_name` — from the sub-issue body

`cycle-runner` returns a compact result block (`VERDICT`, `COMMIT_SHA`, `SUMMARY`, `FINDINGS`).

#### 6d. User checkpoint

Present the result via `AskUserQuestion`:

```
Cycle <cycle> — verdict: <VERDICT>
Summary: <SUMMARY>
Findings:
<FINDINGS>

What would you like to do?
```

Options:
- **Approve** (default-highlighted when `VERDICT: APPROVED`) — continue to 6e.
- **Redo** — re-spawn `cycle-runner` with user-supplied guidance added to the brief (loops back to 6c within the same iteration).
- **Abort** — stop the loop. Leave the working tree as-is (do not roll back committed cycles).

#### 6e. Post-approval

- Close the sub-issue: `gh issue close <sub-issue> --repo kommundsen/Conjecture`.
- If this was the **first** cycle on the branch: go to step 7 (draft PR) before the next iteration. Otherwise push the new commit: `git push origin "$BRANCH"`.

### 7. Draft PR (first cycle only)

After the first approved cycle:

```bash
git push -u origin "$BRANCH"
```

Read `.github/pull_request_template.md`, fill it in based on the whole-enhancement brief from step 3, and open a draft:

```bash
gh pr create --draft \
  --repo kommundsen/Conjecture \
  --title "[<parent>] <enhancement title>" \
  --base main \
  --body "$(cat <<'EOF'
<filled-in pull_request_template.md content>

Part of #<parent>
EOF
)"
```

Capture the PR number for later. Print the URL.

### 8. Final DoD check

After the last sub-issue closes (no open sub-issues remain), spawn `reviewer` with:
- The parent issue body (acceptance criteria / DoD)
- `git diff main...HEAD` (whole-branch diff)

Present verdict via `AskUserQuestion`. Options:
- **Ship it** — proceed to step 9.
- **Address gaps now** — the user identifies one or more gaps; the main thread creates a *synthetic* cycle (treat the gap description as a one-off brief) and loops back to 6c. Repeat until shipworthy.
- **Ship as-is, file follow-ups** — skip to step 9; the user files follow-up issues manually.

### 9. Mark PR ready + final body

Build the per-cycle changelog:
```bash
git log main..HEAD --oneline
```

Update the PR body: re-fill `.github/pull_request_template.md` with the now-complete context and append:

```
Closes #<parent>

## Cycles included
- <sha> <message-line-1>
- <sha> <message-line-2>
…
```

Then:
```bash
gh pr ready <pr-number> --repo kommundsen/Conjecture
gh pr edit <pr-number> --repo kommundsen/Conjecture --body "$(cat <<'EOF'
<new body>
EOF
)"
```

Print the final PR URL.

## Guidelines

- One enhancement per invocation — do not cascade into another parent.
- Branch off `main` (or resume an existing `feat/#<parent>-<slug>`) — never off another feature branch.
- Never roll back previously-committed cycles on the branch.
- Never `git add -A` / `git add .` — stage explicit paths (cycle-runner enforces this internally too).
- Never close the parent issue from this skill; GitHub closes it automatically when the PR merges via `Closes #<parent>`.
- If the parent or any sub-issue references a `/decision` step, invoke the `decision` skill at the appropriate point before that cycle's Red phase.
- If `cycle-runner` returns a non-APPROVED verdict (`RETRIES_EXHAUSTED`, `GREEN_FAILED`, `UNEXPECTED_GREEN`), surface the findings and stop the loop — do not auto-retry from the main thread.

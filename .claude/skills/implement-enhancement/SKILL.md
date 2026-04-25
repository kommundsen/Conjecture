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
  | jq -r --arg p "<parent>" '.[] | select(.title | startswith("[" + $p + "."))'
```

Extract:
- **Parent title** (for the PR title and branch slug)
- **Parent slug**: from parent title, slugify (kebab-case, strip special chars). Cap at 40 chars; trim on word boundary where possible (drop trailing hyphen).
- **Sub-issue list**: every open issue whose title starts with `[<parent>.`, sorted by number ascending

Print a summary line:
```
Enhancement #<parent>: <title>  (<k> open sub-issues)
```

### 2. Fetch contributors (cache for the run)

Bash tool calls do not share shell state, so persist the list to a tmp file inside `.git/`:

```bash
gh api repos/kommundsen/Conjecture/contributors --jq '.[].login' \
  > "$(git rev-parse --git-dir)/conjecture-contributors.txt"
```

Subsequent steps read it back:

```bash
CONTRIBUTORS=$(tr '\n' ' ' < "$(git rev-parse --git-dir)/conjecture-contributors.txt")
```

### 3. Whole-enhancement brief

Spawn the `issue-context` agent with: `issues = [<parent>, <sub1>, <sub2>, …]`, `contributors = $CONTRIBUTORS`.

If the returned brief's `Non-contributor comments to review` section is non-empty, use `AskUserQuestion` to ask the user whether any of those comments require attention before proceeding. Options: "Proceed" / "Pause to address them".

### 4. Branch (create or resume) + detect existing PR

```bash
BRANCH="feat/#<parent>-<slug>"
if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
  git checkout "$BRANCH"                # resume mode
else
  git checkout main && git pull && git checkout -b "$BRANCH"
fi
```

Then detect whether a PR already exists for this branch (resume case):

```bash
EXISTING_PR=$(gh pr list --head "$BRANCH" --repo kommundsen/Conjecture --json number --jq '.[0].number // empty')
```

Capture `$EXISTING_PR` into a tmp file (`"$(git rev-parse --git-dir)/conjecture-pr-number.txt"`) so step 7 can read it. Print whether this is a fresh start, a resume without PR, or a resume with PR `#<EXISTING_PR>`.

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

Track an in-memory `AUTOPILOT` flag (initially `false`); see step 6d.

For each iteration:

#### 6a. Divergence warning + chapter marker + In Progress

Surface (warn-only) if `main` has advanced since the branch was created:

```bash
git fetch origin main --quiet
BEHIND=$(git rev-list --count HEAD..origin/main)
if [ "$BEHIND" -gt 0 ]; then
  echo "⚠️  $BEHIND new commit(s) on main since this branch diverged. Consider rebasing before continuing."
fi
```

Do not auto-rebase. Then:

```text
mcp__ccd_session__mark_chapter title="Cycle <cycle>: <sub-title>"
```
Then `set_in_progress <sub-issue>`.

#### 6b. Per-cycle context brief

Read back `$CONTRIBUTORS` from the tmp file written in step 2. If a draft PR already exists (from `$EXISTING_PR` in step 4 or `$PR_NUMBER` set in step 7), include `pr_number = <n>` so reviewer comments are folded in.

Spawn `issue-context` with `issues = [<sub-issue>]`, `contributors = $CONTRIBUTORS`, and `pr_number` (when known). Capture the full sub-issue body returned in the brief — it is passed to `cycle-runner` so the agent does not re-fetch.

If the brief flags non-contributor comments or unaddressed PR review comments, `AskUserQuestion` before continuing.

#### 6c. /decision check, then run the cycle

If the sub-issue's `## Test` or `## Implement` body mentions `/decision`, invoke the `decision` skill now. This is the single owner for `/decision` invocation — `cycle-runner` does not invoke it.

Spawn `cycle-runner` with:
- `sub_issue_number`
- `cycle_number` (from the sub-issue title, e.g. `76.1`)
- `parent_issue_number`
- `sub_issue_body` — the cached body from 6b (so cycle-runner skips its own `gh issue view`)
- `brief` — the structured output from 6b
- `test_file_path` — from the sub-issue body
- `test_class_name` — from the sub-issue body

`cycle-runner` returns a compact result block (`VERDICT`, `COMMIT_SHA`, `SUMMARY`, `FILES_TOUCHED`, `FINDINGS`). Possible verdicts: `APPROVED`, `RETRIES_EXHAUSTED`, `GREEN_FAILED`, `UNEXPECTED_GREEN`, `INFRA_FAILURE`.

#### 6d. User checkpoint

If `AUTOPILOT == true` AND `VERDICT == APPROVED`, skip the prompt and proceed to 6e. Otherwise present the result via `AskUserQuestion`:

```
Cycle <cycle> — verdict: <VERDICT>
Summary: <SUMMARY>
Findings:
<FINDINGS>

What would you like to do?
```

Options:
- **Approve** (default-highlighted when `VERDICT: APPROVED`) — continue to 6e.
- **Approve all going forward** — set `AUTOPILOT=true`, then continue to 6e. Subsequent cycles with `VERDICT == APPROVED` will skip this prompt; non-APPROVED verdicts always pause regardless of autopilot.
- **Redo** — re-spawn `cycle-runner` with user-supplied guidance added to the brief (loops back to 6c within the same iteration).
- **Abort** — stop the loop. Leave the working tree as-is (do not roll back committed cycles).

#### 6e. Post-approval

- Close the sub-issue: `gh issue close <sub-issue> --repo kommundsen/Conjecture`.
- If no PR exists yet on this branch (`$PR_NUMBER` and `$EXISTING_PR` both empty): go to step 7 (draft PR) before the next iteration. Otherwise push the new commit: `git push origin "$BRANCH"`.

### 7. Draft PR (only when no PR exists yet)

If `$EXISTING_PR` from step 4 is set, skip this step entirely — the resume case already has a PR. Set `PR_NUMBER=$EXISTING_PR` and continue.

Otherwise, after the first approved cycle on a fresh branch:

```bash
git push -u origin "$BRANCH"
```

Read `.github/pull_request_template.md`, fill it in based on the whole-enhancement brief from step 3, and open a draft:

```bash
PR_URL=$(gh pr create --draft \
  --repo kommundsen/Conjecture \
  --title "[<parent>] <enhancement title>" \
  --base main \
  --body "$(cat <<'EOF'
<filled-in pull_request_template.md content>

Part of #<parent>
EOF
)")
PR_NUMBER=$(echo "$PR_URL" | grep -oE '[0-9]+$')
```

Persist `$PR_NUMBER` to `"$(git rev-parse --git-dir)/conjecture-pr-number.txt"` for later iterations / step 9. Print the URL.

### 8. Final DoD check

After the last sub-issue closes (no open sub-issues remain), spawn `reviewer` with:
- The parent issue body (acceptance criteria / DoD)
- `git diff main...HEAD` (whole-branch diff)

Present verdict via `AskUserQuestion`. Options:
- **Ship it** — proceed to step 9.
- **Address gaps now** — for each gap, file a real follow-up sub-issue and let the main loop pick it up:
  1. Determine the next sub-issue index `<n>` (highest existing `[<parent>.X]` number + 1).
  2. Use `AskUserQuestion` to confirm an auto-drafted body (with `## Test` and `## Implement` sections derived from the gap description) — let the user edit before posting.
  3. `gh issue create --repo kommundsen/Conjecture --title "[<parent>.<n>] <gap title>" --body "<drafted body>"`. Add the parent's labels.
  4. Loop back to **step 6**. The standard re-query at the top of the loop picks up the new sub-issue automatically; `cycle-runner` is invoked normally with the real sub-issue number — there is no "synthetic" / issueless mode.
  5. Repeat from step 8 once all newly-filed gap sub-issues close.
- **Ship as-is, file follow-ups** — skip to step 9; the user files follow-up issues manually.

### 9. CI gate, mark PR ready + final body

Before promoting, surface CI status (best-effort — if no CI is configured, the call returns 0 with no rows and the gate is a no-op):

```bash
gh pr checks "$PR_NUMBER" --repo kommundsen/Conjecture --watch || true
```

If any checks failed, present `AskUserQuestion`:
- **Promote anyway** (default for offline / no-CI scenarios)
- **Pause to investigate** — stop here; user fixes CI manually before re-running the skill from step 9.

Then build the per-cycle changelog:
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
gh pr ready "$PR_NUMBER" --repo kommundsen/Conjecture
gh pr edit "$PR_NUMBER" --repo kommundsen/Conjecture --body "$(cat <<'EOF'
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
- If the parent or any sub-issue references a `/decision` step, this skill (step 6c) is the single owner — `cycle-runner` does not invoke `/decision`.
- If `cycle-runner` returns a non-APPROVED verdict (`RETRIES_EXHAUSTED`, `GREEN_FAILED`, `UNEXPECTED_GREEN`, `INFRA_FAILURE`), surface the findings and pause via 6d — do not auto-retry from the main thread. `AUTOPILOT` is ignored for non-APPROVED verdicts.

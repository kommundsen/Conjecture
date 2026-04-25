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

## Architecture

The skill (running in the **main thread**) directly orchestrates each cycle's Red→Green→Review→Commit phases by spawning the leaf agents `test-developer`, `developer`, and `reviewer` one at a time. **There is no `cycle-runner` intermediary** — Claude Code subagents cannot reliably spawn further subagents, so the orchestration must live in the main thread.

The main thread's per-cycle footprint must stay tiny so 10+ cycles fit comfortably:

- **Each leaf agent does its own heavy I/O inside its own context** (`gh issue view`, `dotnet format`, `dotnet build`, `dotnet test`, `git diff`). The main thread never sees that output.
- **Each leaf agent returns ONLY a short structured summary block** (≤12 lines). The summary is what bubbles up to the main thread.
- The main thread holds: parent #, branch, contributors list, current cycle #, last cycle's summary line. Persistent state lives in `$(git rev-parse --git-dir)/conjecture-*` files, not in conversation memory.
- Call `mcp__ccd_session__mark_chapter` before every cycle so compaction has natural boundaries.

Worst-case spawn budget per cycle: 1 `test-developer` × 3 outer iterations + 2 `developer` × 3 outer iterations + 1 `reviewer` × 3 outer iterations + (optional) 1 `issue-context` precheck = 18–19 spawns per cycle. Typical APPROVED-on-first-try with no fresh comments is 3.

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

Print one summary line:
```
Enhancement #<parent>: <title>  (<k> open sub-issues)
```

### 2. Cache contributors for the run

Bash tool calls do not share shell state — persist into `.git/`:

```bash
gh api repos/kommundsen/Conjecture/contributors --jq '.[].login' \
  > "$(git rev-parse --git-dir)/conjecture-contributors.txt"
```

### 3. Whole-enhancement brief (one-time, optional)

Spawn the `issue-context` agent ONCE with `issues = [<parent>]`, `contributors = $CONTRIBUTORS` to surface non-contributor comments on the parent. **Do not** include sub-issue numbers here — each cycle's `test-developer` and `developer` will fetch their own sub-issue body, so re-fetching them in the main thread wastes tokens.

If the brief flags non-contributor parent-issue comments, `AskUserQuestion`: "Proceed" / "Pause to address them".

### 4. Branch (create or resume) + detect existing PR

```bash
BRANCH="feat/#<parent>-<slug>"
if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
  git checkout "$BRANCH"
else
  git checkout main && git pull && git checkout -b "$BRANCH"
fi

EXISTING_PR=$(gh pr list --head "$BRANCH" --repo kommundsen/Conjecture --json number --jq '.[0].number // empty')
echo "$EXISTING_PR" > "$(git rev-parse --git-dir)/conjecture-pr-number.txt"
```

Print whether this is a fresh start, a resume without PR, or a resume with PR `#<EXISTING_PR>`.

### 5. Mark parent In Progress on the Roadmap project

```bash
set_in_progress() {
  local number=$1
  local url="https://github.com/kommundsen/Conjecture/issues/$number"
  gh project item-add 2 --owner kommundsen --url "$url" > /dev/null 2>&1 || true
  local item_id
  item_id=$(gh project item-list 2 --owner kommundsen --format json --limit 500 \
    --jq ".items[] | select(.content.number == $number) | .id")
  [ -z "$item_id" ] && return 1
  gh project item-edit --project-id PVT_kwHOAAZ3vM4BTArq --id "$item_id" \
    --field-id PVTSSF_lAHOAAZ3vM4BTArqzhAYMcQ \
    --single-select-option-id 47fc9ee4 > /dev/null
}

PARENT_STATUS=$(gh project item-list 2 --owner kommundsen --format json --limit 500 \
  --jq '.items[] | select(.content.number == <parent>) | .status')
if [ "$PARENT_STATUS" != "In Progress" ]; then set_in_progress <parent>; fi
```

(Note: `--limit 500` is required — the project has hundreds of items.)

### 6. Per-cycle loop

Re-query open sub-issues at the start of every iteration. Loop while at least one sub-issue remains.

Track an in-memory `AUTOPILOT` flag (initially `false`); see step 6i.

For each iteration:

#### 6a. Pre-cycle bookkeeping (main thread, ≤10 lines context)

```bash
git fetch origin main --quiet
BEHIND=$(git rev-list --count HEAD..origin/main)
[ "$BEHIND" -gt 0 ] && echo "⚠️  $BEHIND new commit(s) on main — consider rebasing later"
```

Then:
- `mcp__ccd_session__mark_chapter title="Cycle <cycle>: <sub-title>"` — natural compaction boundary.
- `set_in_progress <sub-issue>`.

#### 6b. Comment precheck — conditional `issue-context` spawn

Cheap main-thread scan for new non-contributor commentary on the sub-issue and unseen PR review comments. If both counts are zero (the typical case for freshly-planned sub-issues), skip `issue-context` entirely — saves one spawn and ~50 lines of brief per cycle.

```bash
GITDIR=$(git rev-parse --git-dir)
CONTRIB="$GITDIR/conjecture-contributors.txt"

NON_CONTRIB_COMMENTS=$(gh issue view <sub-issue> --repo kommundsen/Conjecture \
  --json comments --jq '.comments[].author.login' \
  | grep -vxFf "$CONTRIB" | wc -l)

NEW_PR_COMMENTS=0
PR_NUMBER=$(cat "$GITDIR/conjecture-pr-number.txt" 2>/dev/null)
if [ -n "$PR_NUMBER" ]; then
  TOTAL=$(gh api "repos/kommundsen/Conjecture/pulls/$PR_NUMBER/comments" --jq 'length')
  SEEN=$(cat "$GITDIR/conjecture-pr-comments-seen.txt" 2>/dev/null || echo 0)
  NEW_PR_COMMENTS=$((TOTAL - SEEN))
  echo "$TOTAL" > "$GITDIR/conjecture-pr-comments-seen.txt"
fi
```

If `NON_CONTRIB_COMMENTS > 0` OR `NEW_PR_COMMENTS > 0`, spawn `issue-context` with `issues = [<sub-issue>]`, `contributors = $CONTRIBUTORS`, and (when PR exists) `pr_number = $PR_NUMBER`. Surface the brief's `Non-contributor comments to review` and `PR review comments to address` sections via `AskUserQuestion`: "Proceed" / "Pause to address them". The brief itself does NOT need to be threaded into the leaf agents — each fetches the issue body itself; the precheck exists only to gate the user pause.

Otherwise (both zero) skip the spawn silently.

#### 6c. Decision check

If the sub-issue body mentions `/decision`, invoke the `decision` skill now. **This skill is the single owner for `/decision` invocation** — leaf agents never invoke it.

Quick check (single bash call, no body printed to main thread):

```bash
gh issue view <sub-issue> --repo kommundsen/Conjecture --json body --jq '.body' \
  | grep -c '/decision' || true
```

If the count is non-zero, run the `decision` skill and commit the resulting ADR (its own commit, separate from the cycle commit). The skill prompts the user with the relevant context.

#### 6d. RED — spawn `test-developer`

Spawn the `test-developer` agent with:
- `subagent_type`: `test-developer`
- `prompt`: include only `sub_issue_number = <n>`, the parent #, and (on retry) the prior reviewer's `ADD_TEST` findings. The agent fetches the issue body itself.

The agent does its own `dotnet build` (red verification) inside its context and returns a `PHASE: RED` block (≤10 lines) with `RED_STATE`, `TESTS_ADDED`, `TEST_FILE`.

If `RED_STATE = UNEXPECTED_GREEN`, pause and `AskUserQuestion` — the test failed to specify new behavior.

#### 6e. GREEN — spawn `developer` (max 2 attempts per outer iteration)

Spawn the `developer` agent with:
- `subagent_type`: `developer`
- `prompt`: include only `sub_issue_number = <n>`, `test_class_name = <name>` (from the test-developer's RED block), and on retry the previous developer's failure notes or the reviewer's `FIX_IMPLEMENTATION` findings.

The agent does its own `dotnet format`, `dotnet build`, `dotnet test --filter` and returns a `PHASE: GREEN` block (≤10 lines) with `RESULT`, `TESTS_PASSED`, `PROD_FILES`.

If `RESULT = FAIL`, re-spawn once more with the failure summary appended. If still `FAIL`, pause and `AskUserQuestion` (`GREEN_FAILED`).

#### 6f. REVIEW — spawn `reviewer`

Spawn the `reviewer` agent with:
- `subagent_type`: `reviewer`
- `prompt`: include only `sub_issue_number = <n>`. The agent fetches the diff, runs format-verify and the deterministic test scope itself.

Returns a `PHASE: REVIEW` block (≤12 lines) ending with `VERDICT: <APPROVED | FIX_IMPLEMENTATION | ADD_TEST>` and ≤3 finding bullets.

#### 6g. Outer-iteration handling

Track `OUTER_ITERATION` (starts at 1, incremented on every loopback). Cap at 3.

- `APPROVED` → continue to 6h.
- `FIX_IMPLEMENTATION` → loop back to **6e** with the reviewer's findings; increment `OUTER_ITERATION`.
- `ADD_TEST` → loop back to **6d** with the reviewer's findings; increment `OUTER_ITERATION`.

If `OUTER_ITERATION > 3` without an APPROVED verdict, pause and `AskUserQuestion` (`RETRIES_EXHAUSTED`).

#### 6h. Commit

The reviewer has already verified format and tests. Stage explicit paths only and commit:

```bash
git add <explicit paths from the developer's PROD_FILES + test-developer's TEST_FILE>
git commit -m "$(cat <<'EOF'
<sub-issue title in imperative present, parent prefix dropped>

Closes #<sub-issue>
Part of #<parent>
EOF
)"
```

No `Co-Authored-By` trailer (project convention). Capture `COMMIT_SHA=$(git rev-parse HEAD)` for the cycle summary.

#### 6i. User checkpoint

If `AUTOPILOT == true`, skip the prompt and proceed to 6j.

Otherwise present the result via `AskUserQuestion`:

```
Cycle <cycle> — APPROVED
Files: <count from `git diff --name-only HEAD~1 HEAD | wc -l`>
Findings: <reviewer's findings list, ≤3 bullets>

What would you like to do?
```

Options:
- **Approve** (default) — continue to 6j.
- **Approve all going forward** — set `AUTOPILOT=true`, then continue to 6j.
- **Redo** — loop back to 6d with user-supplied guidance prepended to the test-developer prompt.
- **Abort** — stop. Leave the working tree as-is (do not roll back committed cycles).

Non-APPROVED phase verdicts (`UNEXPECTED_GREEN`, `GREEN_FAILED`, `RETRIES_EXHAUSTED`) always pause regardless of `AUTOPILOT`.

#### 6j. Post-approval

```bash
gh issue close <sub-issue> --repo kommundsen/Conjecture
```

If no PR exists yet on this branch (`$PR_NUMBER` empty AND `$EXISTING_PR` empty): go to **step 7** (draft PR) before the next iteration. Otherwise:

```bash
git push origin "$BRANCH"
```

### 7. Draft PR (only when no PR exists yet)

If `$EXISTING_PR` from step 4 is set, skip — set `PR_NUMBER=$EXISTING_PR` and continue.

Otherwise, after the first approved cycle on a fresh branch:

```bash
git push -u origin "$BRANCH"
```

Read `.github/pull_request_template.md`, fill it in based on the parent issue's title and body, and open a draft:

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
echo "$PR_NUMBER" > "$(git rev-parse --git-dir)/conjecture-pr-number.txt"
```

Print the URL.

### 8. Final DoD check

After the last sub-issue closes (no open sub-issues remain), spawn `reviewer` with:
- `subagent_type`: `reviewer`
- `prompt`: `parent_issue_number = <parent>`, `mode = dod`, plus the parent's body inline so the agent has the acceptance criteria. The agent runs `git diff main...HEAD` itself and returns a `PHASE: DOD` block.

Present verdict via `AskUserQuestion`:
- **Ship it** → step 9.
- **Address gaps now**: for each gap, file a real follow-up sub-issue and let the main loop pick it up:
  1. Determine the next sub-issue index `<n>` (highest existing `[<parent>.X]` number + 1).
  2. `AskUserQuestion` to confirm the auto-drafted body (`## Test` and `## Implement` sections derived from the gap).
  3. `gh issue create --repo kommundsen/Conjecture --title "[<parent>.<n>] <gap title>" --body "<drafted body>"`. Inherit parent's labels.
  4. Loop back to **step 6**.
- **Ship as-is, file follow-ups** → step 9; user files manually.

### 9. CI gate, mark PR ready + final body

```bash
gh pr checks "$PR_NUMBER" --repo kommundsen/Conjecture --watch || true
```

If checks failed, `AskUserQuestion`:
- **Promote anyway** (default for offline / no-CI scenarios)
- **Pause to investigate**

Then build the per-cycle changelog:
```bash
git log main..HEAD --oneline
```

Update the PR body with the now-complete content from `.github/pull_request_template.md` and append:

```
Closes #<parent>

## Cycles included
- <sha> <message-line-1>
- <sha> <message-line-2>
…
```

```bash
gh pr ready "$PR_NUMBER" --repo kommundsen/Conjecture
gh pr edit "$PR_NUMBER" --repo kommundsen/Conjecture --body "$(cat <<'EOF'
<new body>
EOF
)"
```

Print the final PR URL.

## Guidelines

- One enhancement per invocation.
- Branch off `main` (or resume an existing `feat/#<parent>-<slug>`) — never off another feature branch.
- Never roll back previously-committed cycles on the branch.
- Never `git add -A` / `git add .` — stage explicit paths only.
- Never close the parent issue from this skill; GitHub does it on PR merge via `Closes #<parent>`.
- `decision` skill: this skill (step 6c) is the single owner; leaf agents never invoke it.
- **Do not** print the full sub-issue body, the full diff, or any build/test output to the main thread. Those live inside the leaf agents — only their structured summary blocks reach you.
- If a leaf agent returns text that is not a structured block (e.g. it explained itself instead of summarising), prepend a stern "Return only the structured block, no prose" reminder and re-spawn once. If it still fails, pause via `AskUserQuestion`.

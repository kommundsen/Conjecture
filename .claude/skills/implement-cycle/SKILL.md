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
# List open issues whose title contains the parent prefix, sorted by number
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

### 2. Create a branch

Branch name format: `feat/#<parent>-#<sub>-<slug>`

Example: issue `[76.1] DataGen API in Conjecture.Core` → `feat/76-86-datagen-api`

```bash
git checkout main
git pull
git checkout -b feat/<parent>-<sub>-<slug>
```

### 3. Red phase — write failing tests

Invoke the `test` skill with:
- The behavior description from the **## Test** section of the issue
- The test file path specified in the **## Test** section

Run `dotnet build src/` — must fail or have test failures (red). If unexpectedly green, stop and report.

### 4. Green phase — implement

Invoke the `implement` skill with the test class name extracted from the test file path.

Run `dotnet test src/ --filter "FullyQualifiedName~<TestClassName>"`.

If tests fail, invoke `implement` again with the failing test output as additional context. Repeat until all targeted tests pass or 3 attempts have been made. If still failing after 3 attempts, stop and report what remains failing.

### 5. Refactor phase — simplify

Invoke the `simplify` skill on the production files created or modified during the Green phase.

Run `dotnet test src/ --filter "FullyQualifiedName~<TestClassName>"` again — must still be green.

### 6. Verify no regressions

Run `dotnet test src/` — full suite must be green.

If any previously-passing test now fails: stop, report the regression, and do NOT proceed.

### 7. PublicAPI check

If the **## Implement** section mentions new public API surface, verify `PublicAPI.Unshipped.txt` was updated with the new symbols. If not, update it now.

### 8. Commit

Invoke the `commit-message` skill to generate a suggested commit message.

Stage all new and modified files from this cycle and commit with the suggested message (no `Co-Authored-By` trailer).

### 9. Create PR

```bash
gh pr create \
  --repo kommundsen/Conjecture \
  --title "[<cycle>] <title>" \
  --base main \
  --body "$(cat <<'EOF'
## Summary
<1–3 bullet points from the Implement section>

## Test plan
- [ ] All cycle tests pass (`dotnet test src/ --filter "FullyQualifiedName~<TestClass>"`)
- [ ] Full suite green (`dotnet test src/`)
- [ ] PublicAPI.Unshipped.txt updated (if applicable)

Closes #<sub-issue-number>
Part of #<parent-issue-number>
EOF
)"
```

The `Closes #<sub-issue-number>` line causes GitHub to automatically close the sub-issue when this PR is merged into main.

Print the PR URL.

## Guidelines

- One cycle per invocation — do not cascade into the next cycle.
- If the issue references a `/decision` step, invoke the `decision` skill before implementing.
- Never create the PR if the build or tests are red.
- Scope all changes to what the cycle issue demands.
- Branch off `main` — never off another feature branch.

---
name: plan-issue
description: >
  Plan a GitHub issue for the Conjecture project: resolve design decisions and open questions
  interactively with recommendations, post the answers as issue comments, then break the work
  into linked sub-issues ready for `implement-cycle` to iterate through.
  Use this skill whenever the user wants to plan or break down a GitHub issue, resolve design
  decisions, create sub-issues, set up an implementation roadmap, or says things like
  "let's start on issue #N", "plan issue #N", "break #N into cycles", "what do we need to decide
  for #N", or "set up the next enhancement #N".
---

Turn a GitHub issue with open design questions into a set of linked, implementation-ready
sub-issues sized for `implement-cycle`.

## Input

Issue number, e.g. `/plan-issue 72`.

The repo is always `kommundsen/Conjecture`.

## Step 1 — Fetch and parse the issue

```bash
gh issue view <number> --repo kommundsen/Conjecture
```

Extract:
- **Labels** — check for `needs-adr`
- **Milestone** and **existing labels** (you'll reuse both on sub-issues)
- **"Design Decisions to Make" section** — numbered list of decisions
- **"Open Questions" section** — bulleted or numbered list of questions

If neither decisions nor open questions section exists, skip to Step 4.

## Step 2 — Explore the codebase

Before forming any recommendations, spawn an **Explore subagent** focused on what the
decisions actually touch. Good recommendations come from the code — not from the issue text alone.

Tell the subagent specifically what to find: relevant classes, existing patterns, settings
structure, related commands, test conventions. Ask for file paths, method signatures, and
anything that constrains the design choices.

## Step 3 — Ask design decisions one at a time

For each item under "Design Decisions to Make":

1. Think through the trade-offs using what you found in Step 2.
2. Pick a concrete recommendation and know *why* it's better in this codebase.
3. Call `AskUserQuestion` with:
   - Your recommended option **first**, labelled "(Recommended)"
   - 2–3 alternatives (not exhaustive — pick the realistic ones)
   - Previews showing each option as actual code, config, or CLI syntax
4. Record the answer before moving to the next decision.

Ask **one decision at a time** — never batch them.

## Step 4 — Ask open questions one at a time

Same pattern as Step 3 for every item under "Open Questions". If a question has no clear
best answer, recommend the option that minimises scope or defers complexity — and say so.

## Step 5 — Post decisions as an issue comment

Post **one comment** with all design decisions:

```markdown
## Design Decisions (resolved)

| # | Decision | Choice |
|---|----------|--------|
| 1 | [decision text] | **[chosen option]** — [one-line rationale] |
```

```bash
gh issue comment <number> --repo kommundsen/Conjecture --body "..."
```

## Step 6 — Post open question answers as a separate comment

```markdown
## Open Questions (resolved)

| Question | Answer |
|----------|--------|
| [question] | [answer + one-line rationale] |
```

Post as a **second, separate comment** on the same issue.

## Step 7 — Design the sub-issues

Decompose the work into independently-completable chunks. Each sub-issue will become one
`implement-cycle` session, so scope it to a single cohesive concern: one new class, one
CLI command, one settings block, one integration point.

**Title format** (required — `implement-cycle` parses this):
```
[<parent>.<N>] <ClassName or Command>: <verb what it does>
```
Examples: `[72.1] ValueRenderer: emit C# literal declarations`

**If `needs-adr` label is present**, number the ADR sub-issue **0** so it sorts first:
```
[<parent>.0] ADR: <short decision title>
```

Sub-issue **body template**:
```markdown
Part of #<parent>.

## Dependencies
- #<number> (<title>)   ← omit if no dependencies

## Implement

<What production code to write: class name, file path, public API shape, key logic>

## Test

<What to verify: file path, specific behaviours to cover>
```

For the ADR sub-issue, the `## Implement` section should say:
```
Invoke the `decision` skill to record the architecture decision for [feature].
Key points to document: [bullet the main decisions already resolved in Steps 3–4]
```
No `## Test` section for ADR sub-issues.

**Sequencing rules:**
- Dependencies flow forward — later sub-issues depend on earlier ones, never the reverse.
- Shared infrastructure (a renderer, a base class) comes before anything that uses it.
- CLI commands come after the core logic they call.
- MCP/integration changes come last.

## Step 8 — Create and link sub-issues

Create each sub-issue, reusing the parent's milestone and labels (minus `needs-adr`):

```bash
gh issue create --repo kommundsen/Conjecture \
  --title "[<parent>.<N>] <title>" \
  --label "<labels>" \
  --milestone "<milestone>" \
  --body "..."
```

Then formally link each to the parent via the GitHub API:

```bash
gh api repos/kommundsen/Conjecture/issues/<parent>/sub_issues \
  --method POST \
  --field sub_issue_id=$(gh api repos/kommundsen/Conjecture/issues/<sub> --jq '.id')
```

> Note: `jq` may not be available — use `--jq '.id'` in the `gh api` call directly (gh has
> built-in jq support).

## Step 9 — Add a tasklist comment to the parent issue

```markdown
## Sub-issues

\`\`\`[tasklist]
### Implementation Cycles
- [ ] #N [title]
- [ ] #M [title]
\`\`\`
```

Post this as a comment on the parent issue. Done.

## What makes a good sub-issue

A well-scoped sub-issue:
- Has one clearly-named output (a class, a command, a test file)
- Has an `## Implement` section specific enough that a developer agent can act on it
- Has a `## Test` section listing concrete behaviours — not just "test it"
- Is neither a one-liner (too small) nor an entire subsystem (too large)
- Doesn't duplicate work that belongs to another sub-issue

Vague sub-issues (e.g., "Wiring and integration") make `implement-cycle` produce vague code.
Invest the time to be specific — it pays off in every subsequent cycle.

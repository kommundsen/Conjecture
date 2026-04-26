---
name: plan-idea
description: >
  Promote a fully-interrogated and researched GitHub Discussion thread into a parent
  enhancement issue plus linked sub-issues, ready for `implement`. Synthesizes the
  discussion's idea, decisions, and research findings into the parent body, then reuses
  steps 7–9 of `plan-issue` (sub-issue design, creation, tasklist) — skipping the
  decision/question interrogation since `ideation` and `research-idea` already resolved
  them. Final stage of the chain `ideation` → `research-idea` → `plan-idea`.
  Use this skill whenever the user wants to graduate a Discussion into actual work,
  create the enhancement issue from a discussion, or says things like "promote discussion
  #N to an issue", "plan idea #N", "turn #N into issues", or "/plan-idea #N".
---

Take a Discussion that has been sharpened by `ideation` and validated by `research-idea`,
synthesize the parent enhancement issue body, create the issue, then break it into
sub-issues by reusing `plan-issue`'s later steps verbatim.

## Input

Discussion reference (required): `#N`, full URL, or bare `N`.

The repo is always `kommundsen/Conjecture`.

## Shared infrastructure

Reuse the cache convention from `ideation`:

- `$(git rev-parse --git-dir)/conjecture-ideation-discussion-id.txt`
- `$(git rev-parse --git-dir)/conjecture-ideation-discussion-number.txt`
- `$(git rev-parse --git-dir)/conjecture-ideation-answers.json` (if present)

If the cached number matches the input, skip the fetch in Step 1. Otherwise refetch and
overwrite.

Call `mcp__ccd_session__mark_chapter` once at the start so the parent-issue body
synthesis and the sub-issue creation phase have separate compaction boundaries.

## Step 1 — Resolve and fetch the thread

Parse the input to a number `N`. Fetch:

```bash
gh api graphql -F n=<N> -f query='
  query($n:Int!) {
    repository(owner:"kommundsen", name:"Conjecture") {
      discussion(number:$n) {
        id title url body category{ name }
        comments(first:100) {
          nodes { author{ login } body createdAt url }
        }
      }
    }
  }'
```

Cache `discussion.id` and `discussion.number`.

## Step 2 — Verify readiness

The discussion should already contain:

- An **interrogated** state (a synthesis-style comment from `ideation`, or comparable
  resolution of problem / scope / alternatives / constraints / success criteria).
- A **research findings** comment from `research-idea` with a feasibility verdict.

Print a one-screen readiness check:

```
Discussion #<N>: <title>
- Synthesis comment present:        ✓ / ✗
- Research findings comment present: ✓ / ✗
- Feasibility verdict:               feasible / caveats / not feasible / missing
- Open questions still standing:     <count>
```

Then `AskUserQuestion`:

- *Promote (Recommended)* — proceed even if some boxes are unchecked, on the user's call.
- *Run /ideation first* — abort; user wants more interrogation.
- *Run /research-idea first* — abort; user wants research before planning.
- *Cancel* — abort.

Only continue on *Promote*.

## Step 3 — Synthesize the parent issue body

Build the body from the thread. Use this template (omit empty sections):

```markdown
## Summary

<2–4 sentence problem + approach, sourced from the synthesis comment>

## Motivation

<who's affected and why this matters, from the discussion body / synthesis>

## Scope

**In scope:**
- <bullets>

**Out of scope:**
- <bullets>

## Approach

<paragraph or bullets describing the chosen approach, sourced from synthesis +
research-recommended refinement>

## Design decisions (already resolved)

| # | Decision | Choice |
|---|----------|--------|
| 1 | <decision> | **<choice>** — <one-line rationale> |

## Open questions

- [ ] <question still standing>
- [ ] <question still standing>

## Research notes

Key findings from `/research-idea`:

- **What's already in the codebase:** <bullets with file paths>
- **Patterns to follow:** <bullets with file paths>
- **Prior art consulted:** <names + URLs>
- **Risks surfaced:** <bullets>

## Acceptance criteria

- [ ] <success criterion>
- [ ] <success criterion>

## Source

Promoted from discussion <URL>.
```

Print the draft as a fenced code block.

## Step 4 — Choose labels and milestone

`AskUserQuestion` for each, with sensible defaults pulled from the discussion category
and any labels mentioned in the thread:

- **Labels** (multiSelect): `enhancement` (default), `needs-adr` (if the discussion
  flagged an architectural decision worth an ADR), domain labels mentioned in the thread
  (`generators`, `shrinkers`, `mcp`, `docs`, etc.).
- **Milestone** (single-select): list current open milestones via
  `gh api repos/kommundsen/Conjecture/milestones --jq '.[] | {number, title}'` and offer
  the most recent + "None".

## Step 5 — Approve and create the parent issue

`AskUserQuestion`:

- *Create issue (Recommended)*
- *Edit body* — accept replacement via "Other"
- *Edit title* — accept replacement via "Other"
- *Cancel*

On approval, write the body to a temp file (avoids quoting hazards) and create:

```bash
TMP=$(mktemp)
cat > "$TMP" <<'BODY'
<issue body markdown>
BODY
gh issue create --repo kommundsen/Conjecture \
  --title "<title>" \
  --label "<comma-separated labels>" \
  --milestone "<milestone or omit flag>" \
  --body-file "$TMP"
rm -f "$TMP"
```

Cache the new parent number to `$(git rev-parse --git-dir)/conjecture-plan-idea-parent.txt`
for use in Step 6 and the reused plan-issue steps below.

Add the parent issue to the Conjecture Roadmap project:

```bash
gh project item-add 2 --owner kommundsen \
  --url "https://github.com/kommundsen/Conjecture/issues/<parent>"
```

## Step 6 — Cross-link the discussion

Post a comment on the discussion linking to the new issue so the thread isn't orphaned:

```bash
GIT_DIR=$(git rev-parse --git-dir)
DISCUSSION_ID=$(cat "$GIT_DIR/conjecture-ideation-discussion-id.txt")
PARENT=$(cat "$GIT_DIR/conjecture-plan-idea-parent.txt")
TMP=$(mktemp)
cat > "$TMP" <<BODY
## Promoted to issue

This idea has been planned and graduated to issue #${PARENT}: <https://github.com/kommundsen/Conjecture/issues/${PARENT}>

Sub-issues are being created next; the parent will carry the implementation tasklist.
BODY
gh api graphql \
  -f discussionId="$DISCUSSION_ID" \
  -F body=@"$TMP" \
  -f query='
    mutation($discussionId:ID!, $body:String!) {
      addDiscussionComment(input:{discussionId:$discussionId, body:$body}) {
        comment { url }
      }
    }'
rm -f "$TMP"
```

## Step 7 — Reuse plan-issue's sub-issue flow

From here on, the work is identical to creating sub-issues for any planned issue — so
**do not duplicate the logic**. Read `.claude/skills/plan-issue/SKILL.md` and execute
**Steps 7, 8, and 9** of that skill against the parent issue number from Step 5:

- **Step 7 (plan-issue)** — Design the sub-issues: title format `[<parent>.<N>] …`, body
  template with `## Implement` / `## Test` / `## Dependencies`, ADR sub-issue numbered
  `0` if `needs-adr` is on the parent, then MCP and docs cross-cutting sub-issues per
  the sequencing rules.
- **Step 8 (plan-issue)** — Create each sub-issue, formally link it to the parent via
  `gh api repos/.../sub_issues`, and add every issue to project `2`.
- **Step 9 (plan-issue)** — Post the tasklist comment to the parent.

The parent's "Design decisions (already resolved)" section in Step 3 means you should
**skip plan-issue's Steps 3 and 4** (decision/question interrogation) entirely — those
were resolved during `ideation`. Use the resolved decisions to inform sub-issue scoping
in plan-issue's Step 7.

If the discussion's research notes pointed at specific files/patterns, surface those in
each sub-issue's `## Implement` section so `developer` agents have concrete pointers
without re-exploring.

## Step 8 — Print recommended next skill

Single line, e.g.:

```
Next: /implement #<parent>
```

## What makes a good plan-idea pass

A useful promotion:

- The parent issue body is **self-contained** — anyone reading it cold understands the
  problem, scope, decisions, risks, and acceptance criteria without opening the
  discussion. The discussion is a footnote, not a prerequisite.
- Sub-issues are **specific** about file paths and patterns drawn from the research
  comment — not generic ("add the feature").
- The discussion is **cross-linked** to the issue so the thinking history isn't lost.
- Decisions made in `ideation` are **embedded** in the parent body, not re-asked in
  plan-issue's Step 3/4 flow.

If the readiness check in Step 2 showed missing synthesis or feasibility, prefer
suggesting `/ideation` or `/research-idea` over forcing the promotion — bad parent
issues breed bad sub-issues.

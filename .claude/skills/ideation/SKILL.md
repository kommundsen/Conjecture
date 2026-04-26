---
name: ideation
description: >
  Articulate, interrogate, and record an idea on a GitHub Discussion thread before it
  becomes a concrete enhancement issue. Two modes: with a discussion ref, interrogate the
  thread and propose follow-up comments; without a ref, collaborate on a first draft, post
  it to the Ideas category, then offer to enter interrogation mode.
  Use this skill whenever the user wants to brainstorm, develop, or pressure-test an idea,
  or says things like "I have an idea about X", "let's brainstorm Y", "ideate on Z",
  "interrogate discussion #N", "develop the idea in #N", or "I want to think through X".
---

Own the fuzzy front end of feature work — articulate an idea, interrogate it, and record it
on a GitHub Discussion so it has a permanent home before it graduates into an enhancement
issue. Entry point of the chain `ideation` → `research-idea` → `plan-idea`.

## Input

- Optional discussion reference: `#N`, `https://github.com/kommundsen/Conjecture/discussions/N`,
  or bare `N`.
- Optional free-form idea text.

If a discussion ref is detected, run **Mode A**. Otherwise, run **Mode B**.

The repo is always `kommundsen/Conjecture`.

## Shared infrastructure

Cache long-lived state under `$(git rev-parse --git-dir)/` so it survives Bash calls and
compaction:

- `conjecture-repo-id.txt` — repository node ID for `createDiscussion`
- `conjecture-ideation-discussion-id.txt` — current discussion node ID
- `conjecture-ideation-discussion-number.txt` — current discussion number
- `conjecture-ideation-answers.json` — accumulated interrogation answers `{category: answer}`

Resolve the repo node ID once per run if absent:

```bash
GIT_DIR=$(git rev-parse --git-dir)
test -s "$GIT_DIR/conjecture-repo-id.txt" || \
  gh api repos/kommundsen/Conjecture --jq .node_id > "$GIT_DIR/conjecture-repo-id.txt"
```

Discussion category IDs:

| Category | ID |
|---|---|
| Ideas (default) | `DIC_kwDORwxDGc4C5-Df` |
| General | `DIC_kwDORwxDGc4C5-Dd` |
| Q&A | `DIC_kwDORwxDGc4C5-De` |
| Show and tell | `DIC_kwDORwxDGc4C5-Dg` |
| Announcements | `DIC_kwDORwxDGc4C5-Dc` |
| Polls | `DIC_kwDORwxDGc4C5-Dh` |

Call `mcp__ccd_session__mark_chapter` at the start of Mode A and Mode B for clean
compaction boundaries.

When passing multi-line discussion bodies to `gh api graphql`, write the body to a temp
file and reference it with `-F body=@<file>` — avoids quoting hazards.

---

## Mode A — interrogate an existing discussion

### Step A1 — Resolve and fetch the thread

Parse the input to a number `N`. Fetch:

```bash
gh api graphql -F n=<N> -f query='
  query($n:Int!) {
    repository(owner:"kommundsen", name:"Conjecture") {
      discussion(number:$n) {
        id title body category{ name }
        comments(first:50) {
          nodes { author{ login } body createdAt }
        }
      }
    }
  }'
```

Cache `discussion.id` and `discussion.number` to the `.git/conjecture-ideation-*.txt`
files. Read existing `conjecture-ideation-answers.json` if present (resume case).

### Step A2 — Synthesize current state

Print a compact brief (≤300 words) in this shape:

```
**Title:** <title>
**Category:** <category>

**Goals**
- …

**What's resolved**
- …

**Open questions**
- …

**Comment timeline**
- @login (date): <one-line summary>
```

Build it from the body + comment authors/bodies. Skip resolved categories in Step A3.

### Step A3 — Interrogate, one question at a time

For each category below, if the brief shows it is **not** already resolved:

1. Problem clarity — what specific pain does this solve, and who experiences it?
2. Scope — what's in scope vs deferred?
3. Alternatives — what else was considered, and why this approach?
4. Constraints — perf, public API surface, compat, deps, tooling?
5. Success criteria — how will we know it worked?
6. Remaining unknowns — what does the user still not know?

For each, call `AskUserQuestion`:

- Phrase one focused question.
- Provide 2–3 plausible options as multiple choice; the user can always pick "Other".
- Where the codebase informs the choice, briefly mention what you found (don't speculate).

Persist answers after each round:

```bash
GIT_DIR=$(git rev-parse --git-dir)
# update $GIT_DIR/conjecture-ideation-answers.json with {category: answer}
```

Ask **one category at a time** — never batch.

### Step A4 — Draft follow-up comments

Synthesize the answers into up to three drafts:

**Synthesis** — restates the idea with new clarity. Body shape:
```markdown
## Idea (refined)

<2–4 sentences capturing the sharpened idea>

**Problem:** <…>
**Approach:** <…>
**Out of scope:** <…>
**Success criteria:** <…>
```

**Open questions** — only if any unknowns remain:
```markdown
## Still open

- [ ] <question>
- [ ] <question>
```

**Next steps** — recommend exactly one of: `research-idea`, `plan-idea`, "needs more
thought", or "discard". Body:
```markdown
## Suggested next step

`/research-idea #<N>` — <one-line rationale>
```

Print each draft in a fenced code block. For each, call `AskUserQuestion`:

- *Post as-is (Recommended)*
- *Edit*
- *Skip*

On *Edit*, ask via `AskUserQuestion` "Other" for the replacement body.

### Step A5 — Post approved comments

For each approved draft, write the body to a temp file then post:

```bash
GIT_DIR=$(git rev-parse --git-dir)
DISCUSSION_ID=$(cat "$GIT_DIR/conjecture-ideation-discussion-id.txt")
TMP=$(mktemp)
cat > "$TMP" <<'BODY'
<comment markdown here>
BODY
gh api graphql -f discussionId="$DISCUSSION_ID" -F body=@"$TMP" -f query='
  mutation($discussionId:ID!, $body:String!) {
    addDiscussionComment(input:{discussionId:$discussionId, body:$body}) {
      comment { url }
    }
  }'
rm -f "$TMP"
```

Print each returned URL.

### Step A6 — Print recommended next skill

Single line, e.g.:

```
Next: /research-idea #<N>
```

No auto-invocation.

---

## Mode B — start from scratch

### Step B1 — Capture the seed

If the user supplied idea text on invocation, use it. Otherwise call `AskUserQuestion`
with question "What's the idea?" and a single "Other" option for free text.

### Step B2 — Lightweight first-draft questions

Ask, one at a time, via `AskUserQuestion`:

1. What problem does this solve?
2. Who's affected?
3. Sketch of a solution (1–2 sentences).
4. Anything already ruled out? (offer "Nothing yet" as an option)

Provide 2–3 plausible options where you can; "Other" is always available.

### Step B3 — Draft the discussion post

Propose 2–3 imperative-noun-phrase **titles** plus "Other"; ask the user to pick.

Body template (omit `Already considered` if empty):

```markdown
## Problem

<B2.1>

## Who's affected

<B2.2>

## Sketch of a solution

<B2.3>

## Already considered

<B2.4>

## Open questions

- <seeded from anything ambiguous in the answers>
```

Print the full draft in a fenced code block. Then `AskUserQuestion`:

- *Post (Recommended)*
- *Edit*
- *Cancel* — abort without posting

### Step B4 — Choose category

`AskUserQuestion` with default **Ideas (Recommended)**, plus *General*, *Q&A*,
*Show and tell* as alternatives. Map the choice to its category ID from the table above.

### Step B5 — Post the discussion

```bash
GIT_DIR=$(git rev-parse --git-dir)
REPO_ID=$(cat "$GIT_DIR/conjecture-repo-id.txt")
TMP=$(mktemp)
cat > "$TMP" <<'BODY'
<post markdown here>
BODY
gh api graphql \
  -f repositoryId="$REPO_ID" \
  -f categoryId="<chosen category ID>" \
  -f title="<chosen title>" \
  -F body=@"$TMP" \
  -f query='
    mutation($repositoryId:ID!, $categoryId:ID!, $title:String!, $body:String!) {
      createDiscussion(input:{
        repositoryId:$repositoryId,
        categoryId:$categoryId,
        title:$title,
        body:$body
      }) {
        discussion { id number url }
      }
    }'
rm -f "$TMP"
```

Cache the returned `id` and `number` into the `.git/conjecture-ideation-discussion-*.txt`
files. Print the new discussion number and URL.

### Step B6 — Offer interrogation handoff

Call `AskUserQuestion`:

- *Enter interrogation mode now (Recommended)* — jump to **Step A2** using the cached
  discussion data (skip A1, already fetched).
- *Stop here* — exit the skill cleanly.

---

## What makes a good ideation session

A well-run ideation session:

- Produces a discussion thread that **another contributor** could read cold and understand
  the idea, the problem, and what's still open.
- Has a sharpened **synthesis** comment after interrogation — not just a transcript.
- Names a concrete next step (`research-idea`, `plan-idea`, "needs more thought",
  "discard") so the idea doesn't drift.
- Avoids re-asking anything already resolved in the existing thread.

Vague threads (e.g., "we should improve perf") aren't worth promoting to issues. Invest
the interrogation time — it's cheaper here than during `plan-issue`.

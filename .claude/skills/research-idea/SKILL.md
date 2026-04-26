---
name: research-idea
description: >
  Research a GitHub Discussion-resident idea against the codebase and prior art, then post
  a structured "Research findings" comment back to the thread. Spawns Explore agents for
  codebase reconnaissance and WebSearch for external prior art (Hypothesis, FsCheck,
  QuickCheck, papers, etc.). Middle stage of the chain `ideation` → `research-idea` →
  `plan-idea`.
  Use this skill whenever the user wants to validate the feasibility of an idea, gather
  prior art, or check what already exists in the codebase before committing to a plan.
  Triggers on phrases like "research discussion #N", "is idea #N feasible", "look up
  prior art for #N", "what's already in the codebase for #N", or "/research-idea #N".
---

Take a discussion thread that contains a sharpened idea (typically after `ideation`
interrogation), do focused codebase + external research, and post the findings as a
single, well-structured comment so the next skill (`plan-idea`) has solid ground to
plan from.

## Input

Discussion reference (required): `#N`, full URL, or bare `N`.

The repo is always `kommundsen/Conjecture`.

## Shared infrastructure

Reuse the cache convention from `ideation`:

- `$(git rev-parse --git-dir)/conjecture-ideation-discussion-id.txt` — discussion node ID
- `$(git rev-parse --git-dir)/conjecture-ideation-discussion-number.txt` — discussion number
- `$(git rev-parse --git-dir)/conjecture-ideation-answers.json` — interrogation answers, if any

If the cached number matches the input, skip the fetch in Step 1. Otherwise refetch and
overwrite the cache.

Call `mcp__ccd_session__mark_chapter` once at the start so compaction has a clean
boundary across the agent spawns.

When passing multi-line bodies to `gh api graphql`, write to a temp file and reference it
with `-F body=@<file>`.

## Step 1 — Resolve and fetch the thread

Parse the input to a number `N`. Fetch:

```bash
gh api graphql -F n=<N> -f query='
  query($n:Int!) {
    repository(owner:"kommundsen", name:"Conjecture") {
      discussion(number:$n) {
        id title body category{ name }
        comments(first:100) {
          nodes { author{ login } body createdAt }
        }
      }
    }
  }'
```

Cache `discussion.id` and `discussion.number`.

## Step 2 — Identify research questions

Read the thread end-to-end. Build a small **research brief** in three buckets:

- **Codebase questions** — what already exists? what would the idea touch? what patterns
  would it follow or break? (e.g. "is there a `Generate.X` for this already?",
  "where does the Strategy interface live?", "is there a similar shrinker?").
- **Prior-art questions** — what does Hypothesis (Python), FsCheck, QuickCheck, jqwik,
  ScalaCheck, or fast-check do here? Are there papers or blog posts that name the
  trade-offs?
- **Risk / feasibility questions** — perf, public API surface, allocation, compat with
  existing PublicAPI baselines, dep weight.

Print the brief to the user. Use `AskUserQuestion`:

- *Looks right — proceed (Recommended)*
- *Add or refine questions* — accept additions via "Other"
- *Cancel* — abort without spawning anything

## Step 3 — Spawn research agents in parallel

Send all spawns in **one message** so they run concurrently. Cap at three.

**Codebase agent** — `subagent_type: Explore`, thoroughness `medium` or `very thorough`
depending on scope. Prompt must include the idea title, the codebase questions verbatim,
and ask for: file paths, type/method signatures, current patterns, gaps, and concrete
"this is what would change" pointers. Ask for ≤400 words back.

**Prior-art agent** — `subagent_type: general-purpose`. Prompt: list the libraries/papers
to compare (Hypothesis is usually the primary reference for this repo), ask the agent to
use `WebSearch` and `WebFetch` to gather what each does, summarise the design choices,
and call out where they disagree. Ask for ≤400 words back, with URLs.

**(Optional) Risk agent** — only if the brief lists ≥3 distinct risk questions. Otherwise
fold risk into the codebase agent's prompt. `subagent_type: Explore` focused on perf-/
allocation-sensitive call sites and PublicAPI files.

Each agent returns a short structured summary; the main thread holds only those
summaries (not their search transcripts).

## Step 4 — Synthesize the findings comment

Compose **one** comment in this shape (omit empty sections):

```markdown
## Research findings

### Codebase reconnaissance
- **What's already there:** <bullets with file paths>
- **What this would touch:** <bullets with file paths>
- **Patterns to follow:** <bullets with file paths>
- **Gaps:** <bullets>

### Prior art
- **Hypothesis (Python):** <one-paragraph summary> — <URL>
- **FsCheck:** <…> — <URL>
- **<other>:** <…> — <URL>
- **Where they disagree:** <bullets>

### Feasibility
- **Verdict:** *feasible* / *feasible with caveats* / *not feasible as stated*
- **Caveats:** <bullets — perf, API surface, dep weight, compat>
- **Risks worth surfacing:** <bullets>

### Suggested refinement
<1–3 sentences on how the idea should be re-shaped given the findings, or "no change">

### Recommended next step
`/plan-idea #<N>` — *or* "needs more thought" — *or* "discard" — with a one-line reason.
```

Print the draft in a fenced code block.

## Step 5 — Approve and post

`AskUserQuestion`:

- *Post as-is (Recommended)*
- *Edit* — accept replacement body via "Other"
- *Skip posting* — keep the synthesis local only (rare; only useful for throwaway runs)

On approval, post the comment:

```bash
GIT_DIR=$(git rev-parse --git-dir)
DISCUSSION_ID=$(cat "$GIT_DIR/conjecture-ideation-discussion-id.txt")
TMP=$(mktemp)
cat > "$TMP" <<'BODY'
<comment markdown>
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

Print the returned URL.

## Step 6 — Print recommended next skill

Single line, e.g.:

```
Next: /plan-idea #<N>
```

If the verdict was *not feasible* or the recommendation was *needs more thought*, suggest
`/ideation #<N>` to re-interrogate instead.

## What makes a good research-idea pass

A useful findings comment:

- **Cites file paths** for every codebase claim — vague "we already have something
  similar" is worthless.
- **Cites URLs** for every prior-art claim — readers can verify and dig deeper.
- Has a **verdict line** that commits to feasible / caveats / not feasible. No fence-
  sitting.
- Names **risks the idea author hasn't considered yet** — the whole point of this skill
  is to surface what `ideation` couldn't see without searching.
- Suggests a **refinement** when the research changes the picture, instead of silently
  endorsing the original framing.

If two of those four are missing, the comment isn't ready to post — refine before
approving.

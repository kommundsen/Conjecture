# Explanation Guide

Explanation builds understanding. The reader isn't trying to do anything right now — they want to understand why something works the way it does, what tradeoffs were made, or how a concept fits into the bigger picture.

## Core principle

Illuminate, don't instruct. The reader finishes with a richer mental model, not a new skill or a fact to look up.

## Structure

```
# [Concept or "Understanding X"]

[One or two sentences: the question this page answers]

## [Angle 1: the core concept or problem it solves]
...

## [Angle 2: how Conjecture approaches it]
...

## [Angle 3: tradeoffs, alternatives considered, limitations]
...

## Further reading
[Links to tutorials, how-tos, reference, or ADRs]
```

There's no fixed template — explanation pages are essays, not recipes. Structure them around the question the reader brings, not around the API surface.

## Rules for explanation

**Do:**
- Start from the reader's question, not from the implementation
- Use analogy and contrast — compare to familiar concepts (QuickCheck, FsCheck, unit tests)
- Discuss tradeoffs honestly — if there are limitations or design costs, say so
- Reference ADRs (`docs/decisions/`) for the rationale behind design decisions
- Use diagrams or tables when they clarify relationships
- Link to tutorials and how-tos for readers who want to act on the understanding

**Don't:**
- Give step-by-step instructions (that's How-to)
- List API signatures (that's Reference)
- Teach by doing (that's Tutorial)
- Pad with summaries that just repeat what was just said

## Tone

Thoughtful and discursive, but not academic. Active voice. First-person plural is fine ("we" when speaking about design decisions). Write for a developer who has used the library and now wants to understand it more deeply.

## What belongs here

- How shrinking works (the byte-buffer model, why it's different from type-based shrinking)
- Why Conjecture uses a byte buffer rather than generating values directly
- The filter budget and why `Assume` can fail
- The example database — what it stores, why, and the tradeoffs
- Stateful testing — what "command sequence" means and why it finds bugs unit tests miss
- The relationship between `Strategy<T>` and `IStrategyProvider<T>`
- Targeted testing — what a score is, how the engine uses it

## Example opening

```markdown
# Understanding shrinking

When Conjecture finds a failing input, it doesn't stop there — it tries to find a *simpler*
version of the same failure. This process is called shrinking, and it's what turns a
10,000-character string counterexample into `""` or `"a"`.

## Why shrinking matters

A minimal counterexample is a diagnostic tool. When the shrinker hands you `Push(0), Pop`
instead of a 47-command sequence, the bug is immediately obvious. Without shrinking,
property-based testing would still find bugs, but reading the output would be exhausting.

## How Conjecture's shrinker works

Most property testing libraries shrink by type: they know that a smaller integer is simpler,
that a shorter list is simpler. Conjecture takes a different approach...
```

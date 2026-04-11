# How-to Guide

A how-to guide helps the reader accomplish a specific, practical goal. They already know what property-based testing is — they need to know how to do X with Conjecture specifically.

## Core principle

The reader arrives with a task in mind. Get them to the solution as directly as possible. No preamble, no theory.

## Structure

```
# How to [verb phrase]

[One sentence: what this guide achieves. No "In this guide, we will…"]

## [Prerequisite / when to use this]   ← optional, keep short
[Only if the approach has meaningful constraints or alternatives]

## Steps

### 1. [First action]
[Code or command. One sentence of context if non-obvious.]

### 2. [Next action]
...

## [Variant / edge case heading]   ← optional
[Only if there's a meaningfully different approach worth showing]
```

## Rules for how-to guides

**Do:**
- Lead with the solution, not background
- Use numbered steps when order matters
- Show only the code relevant to the task (use `// ...` to elide unchanged surrounding code where context is still clear)
- Include framework tabs when the approach differs across adapters
- Link to Reference or Explanation for deeper context, but don't include it inline

**Don't:**
- Teach concepts (that's Tutorial or Explanation)
- List exhaustive options (that's Reference)
- Start with "First, let's understand what X is…"
- Pad with motivational sentences

## Tone

Terse and confident. Imperative mood. "Pin the seed with `[ConjectureSettings(Seed = …)]`." not "You might want to consider pinning the seed."

## Filename convention

`verb-the-noun.md` — e.g., `reproduce-a-failure.md`, `test-stateful-systems.md`, `filter-generated-values.md`

## Example opening

```markdown
# How to reproduce a failure

Pin the seed from the failure output to replay the exact counterexample every run.

## Steps

### 1. Find the seed

The failure output includes the seed:

```text
Falsified after 47 examples (seed: 9876543210).
```

### 2. Pin it with `[ConjectureSettings]`

```csharp
[Property]
[ConjectureSettings(Seed = 9876543210UL)]
public void MyProperty(int value)
{
    // ...
}
```

The test now replays the same inputs deterministically.

> [!TIP]
> Remove the seed attribute once the bug is fixed so the property resumes random exploration.
```

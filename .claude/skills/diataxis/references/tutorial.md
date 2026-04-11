# Tutorial Guide

A tutorial teaches by doing. The reader doesn't need to understand everything — they need to succeed at something concrete and come away with a working mental model they built through action.

## Core principle

You are the guide; the reader is learning by following. Every step produces a visible result. Theory comes after the reader has seen the thing work, not before.

## Structure

```
# Tutorial N: [Title]

[One sentence: what the reader will build or accomplish]

## Prerequisites
[Minimum viable list — don't over-specify]

## [First concrete step with a visible outcome]
[Show the code. Explain what it does *after* showing it.]

## [Next step that builds on the first]
...

## What you built
[Brief recap — name the concepts they encountered, now that they've seen them]

## Next
[Link to the next tutorial or a relevant how-to]
```

## Rules for tutorials

**Do:**
- Start with working code immediately — the reader should be able to run something by the end of the first section
- Show complete, runnable examples (no `// ...` elisions in the happy path)
- Use framework tabs when the code differs across xUnit v2/v3, NUnit, MSTest
- Explain *what* just happened after each step, not before
- Keep scope narrow — one concept per tutorial
- Number tutorials (`01-`, `02-`, etc.) — they form a sequence

**Don't:**
- Explain how the engine works internally (that's Explanation)
- List every overload or option (that's Reference)
- Assume the reader will go off-script — handle the happy path only
- End without a "what's next" pointer

## Tone

Warm, direct, active voice. "Write a property test." not "A property test can be written by…". The reader is a capable developer who is new to property-based testing, not new to C#.

## Numbering

Check the existing `tutorials/toc.yml` for the highest number and increment. New tutorials go at the end unless they logically precede an existing one.

## Example opening

```markdown
# Tutorial 3: Custom Strategies

In this tutorial you'll write a strategy that generates valid `EmailAddress` values,
then use it in a property test.

## Prerequisites

- Completed [Tutorial 1](01-your-first-property-test.md) and [Tutorial 2](02-strategies-and-composition.md)
- A test project with a Conjecture adapter installed

## Define a strategy
```

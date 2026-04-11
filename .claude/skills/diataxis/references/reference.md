# Reference Guide

Reference material is consulted, not read. The reader knows what they're looking for — they need accurate, complete information with no filler.

## Core principle

Describe the thing precisely. Don't explain why it works or teach how to use it. The reader will come here to verify a type signature, find an option name, or check a constraint.

## Structure

```
# [Type / API surface / config area name]

[One sentence: what this is. No more.]

## [Property / Method / Option name]

**Type:** `T`
**Default:** `value`

[One sentence description. Link to how-to or explanation if there's non-obvious usage.]

| Value | Meaning |
|---|---|
| ... | ... |
```

Use tables heavily. Use definition lists for properties and settings. Every item gets its own heading.

## Rules for reference

**Do:**
- Cover the complete public API surface for the subject — omissions are bugs
- State types, defaults, constraints, and valid ranges precisely
- Use the exact C# type names as they appear in the public API
- Keep cross-references to how-to and explanation — don't embed guidance inline
- Group by namespace or logical area, not alphabetically (unless that's more natural)

**Don't:**
- Include examples longer than ~5 lines — link to tutorials/how-tos instead
- Explain *why* something is designed the way it is (that's Explanation)
- Include step-by-step instructions (that's How-to)
- Add motivational prose

## Tone

Clinical and precise. Third person or noun phrases. "The `Seed` property pins the random seed used for generation." not "You can use `Seed` to pin your seed."

## What belongs here

- `[ConjectureSettings]` attribute — all properties, types, defaults, constraints
- `Generate.*` strategy methods — signatures, type parameters, overloads
- `Assume` API — methods and their behavior on filter budget exhaustion
- `IStateMachine<TState, TCommand>` interface — all members
- Settings profiles — all named profiles and what they set
- Source generator attributes — `[From<T>]`, `[IStrategyProvider<T>]`

## What doesn't belong here

The DocFX-generated `api/` section covers XML-doc'd public types automatically. Manual reference pages complement it — focus on conceptual groupings, configuration, and anything not well-expressed in API docs alone (e.g., cross-cutting settings, valid value ranges, behavioral contracts).

## Example

```markdown
# ConjectureSettings attribute

Controls per-property engine behavior. Applied to any `[Property]` method.

## Seed

**Type:** `ulong`
**Default:** random (changes each run)

Pins the random seed so the property replays the same inputs deterministically.
Useful for reproducing failures. See [How to reproduce a failure](../how-to/reproduce-a-failure.md).

## MaxExamples

**Type:** `int`
**Default:** `100`

Number of examples to generate before declaring the property passing.
Must be ≥ 1.

| Value | Effect |
|---|---|
| Low (< 10) | Fast; may miss edge cases |
| High (> 10 000) | Thorough; slow in CI |
```

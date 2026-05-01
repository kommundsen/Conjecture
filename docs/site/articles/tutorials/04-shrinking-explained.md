# Tutorial 4: Shrinking Explained

When Conjecture finds a failing test input, it doesn't stop there. It automatically **shrinks** the input to find the smallest, simplest value that still triggers the failure. This tutorial explains how and why.

## Why Shrinking Matters

Suppose your property test fails with this counterexample:

```
List<int> { 847, -29341, 0, 5, 1024, -7, 42, 331, 0, -88, 16 }
```

Which elements matter? Is the length important? The sign? Without shrinking, you'd waste time investigating red herrings.

With shrinking, Conjecture reduces this to something like:

```
List<int> { 1 }
```

Now the bug is obvious — the code fails on single-element lists.

## How It Works

Conjecture uses **byte-stream shrinking**, the same approach as Python Hypothesis. Every generated value is backed by a byte buffer. Shrinking operates on the bytes, not the values:

1. **Find a failure** — generate examples until one fails.
2. **Minimize the byte buffer** — try making the buffer shorter and lexicographically smaller.
3. **Replay through the strategy** — each candidate buffer produces a value via the same strategy.
4. **Keep the smallest failing input** — if the value still fails the property, keep it.

This means shrinking is **universal** — it works for any type, any strategy, with no custom shrinker code.

## Shrinking in Practice

### Numbers Shrink Toward Zero

```csharp
[Property]
public bool Ints_are_small(int value) => Math.Abs(value) < 100;
```

Counterexample: `100` (or `-100`) — the smallest absolute value that violates the property.

### Strings Shrink Toward Short and Simple

```csharp
[Property]
public bool Strings_are_short(string value) => value.Length < 5;
```

Counterexample: `"     "` (5 spaces) or similar — shortest string that violates the length check, using the simplest possible characters.

### Collections Shrink by Removing Elements

```csharp
[Property]
public bool Lists_are_sorted(List<int> items)
{
    for (int i = 1; i < items.Count; i++)
        if (items[i] < items[i - 1]) return false;
    return true;
}
```

Counterexample: `[1, 0]` — the shortest list that isn't sorted, with the smallest values.

## Labels Improve Readability

Use `WithLabel` to name strategy outputs in failure messages:

```csharp
var ageStrategy = Strategy.Integers<int>(0, 150).WithLabel("age");
var nameStrategy = Strategy.Strings(minLength: 1).WithLabel("name");
```

When a failure occurs, the output shows which value is which:

```
Falsifying example:
  age = 0
  name = ""
```

## What You Don't Need to Do

Unlike some property-based testing frameworks, Conjecture does **not** require:

- Writing custom shrinkers per type
- Annotating types with shrink hints
- Choosing between "integrated" vs. "type-based" shrinking

Shrinking is built into the engine. If you can generate it, Conjecture can shrink it.

## Next

[Tutorial 5: Framework Adapters](05-framework-adapters.md) — use Conjecture with xUnit v2, xUnit v3, NUnit, or MSTest.

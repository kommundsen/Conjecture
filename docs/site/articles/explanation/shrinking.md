# Understanding shrinking

When a property test fails, Conjecture doesn't just report the first counterexample it found — it searches for a simpler one. This process is called **shrinking**, and it's what separates a useful property testing tool from a noise generator.

## Why shrinking matters

Suppose your property fails on:

```text
List<int> { 847, -29341, 0, 5, 1024, -7, 42, 331, 0, -88, 16 }
```

Which elements matter? Is the length important? The sign? Without shrinking you'd spend time investigating red herrings.

With shrinking, Conjecture reduces this to something like:

```text
List<int> { 1 }
```

Now the bug is obvious — the code fails on any single-element list.

## How byte-stream shrinking works

Conjecture's engine is built around a key insight: instead of generating values directly, every generated value is read from a **byte buffer**. The strategy for `int` reads some bytes and interprets them as an integer. The strategy for `List<int>` reads a length, then reads that many integers. The strategy for a custom type composed with `Generate.Compose` reads bytes in whatever order the factory function draws them.

This means:
- Every generated value has a corresponding byte buffer
- The same buffer, replayed through the same strategy, always produces the same value
- A shorter or lexicographically smaller buffer often produces a simpler value

Shrinking operates on the buffer, not the value. The shrinker doesn't need to know anything about the type being generated — it just tries to find a smaller buffer that still produces a failing input.

## The 10-pass shrinking architecture

After finding a failing buffer, Conjecture runs up to 10 shrink passes in sequence, repeating until no pass makes progress:

| Pass | What it does |
|---|---|
| ZeroBlocks | Replaces blocks of bytes with zeros |
| DeleteBlocks | Removes blocks of bytes entirely |
| LexMinimize | Tries to reduce bytes lexicographically |
| IntegerReduction | Finds integers in the buffer and reduces them toward zero |
| FloatSimplification | Simplifies floating-point values toward simple rationals |
| StringAware | Applies text-specific reductions (shorter strings, simpler characters) |
| BlockSwapping | Tries reordering adjacent blocks to find a smaller ordering |
| NumericAware | Reduces numeric strings digit-by-digit |
| CommandSequence | Removes steps from stateful test sequences |
| (Additional passes) | Library-specific passes added in later versions |

The passes interact: an `IntegerReduction` pass might enable a subsequent `DeleteBlocks` pass to remove bytes that the smaller integer no longer uses.

## Why universal shrinking works

Earlier property testing frameworks (QuickCheck, FsCheck) use type-directed shrinking: every generator comes with a `shrink` function that knows how to produce "simpler" values of that type. Integers shrink toward zero; lists shrink by removing elements; and so on.

This works well for simple types but breaks down for complex ones. If you build a custom generator by composing simpler generators, you have to write a custom shrinker that understands your composition — and the composition of shrinkers is fragile.

Conjecture avoids this entirely. Because shrinking operates on the byte buffer, any value that can be generated can be shrunk — including types composed with `Generate.Compose`, types involving `SelectMany`, and custom `IStrategyProvider<T>` implementations. The quality of shrinking depends only on how well the byte passes reduce the buffer, not on the complexity of the type.

The practical result: a deeply nested custom object type built from a `Generate.Compose` factory shrinks just as well as a plain integer.

## What shrinking produces

For common types:
- **Integers** shrink toward zero
- **Floats/doubles** shrink toward simple rationals: 0.0, 1.0, -1.0, 0.5
- **Strings** shrink toward shorter strings with simpler characters (spaces, `"a"`)
- **Collections** shrink by removing elements, then shrinking remaining elements
- **Enums** shrink toward the first declared value
- **Recursive structures** shrink toward shallower trees and simpler leaf values
- **State machine runs** shrink by removing commands, then simplifying command arguments

## Seed and reproducibility

Every failure is tied to a seed — the initial state of the PRNG that produced the failing byte buffer. The seed is reported in every failure message:

```text
Falsifying example after 47 examples (seed: 0xDEADBEEF12345678):
  value = 42
```

The *shrunk* counterexample is the one shown — the original failing input may have been much larger. To reproduce the exact sequence that led to the failure (not just the shrunk counterexample), pin the seed:

```csharp
[Property(Seed = 0xDEADBEEF12345678)]
public bool My_property(int value) => ...;
```

## Further reading

- [Tutorial 4: Shrinking Explained](../tutorials/04-shrinking-explained.md) — interactive walkthrough
- [How to reproduce a failure](../how-to/reproduce-a-failure.md)
- [How to generate recursive structures](../how-to/generate-recursive-structures.md) — how recursive shrinking works

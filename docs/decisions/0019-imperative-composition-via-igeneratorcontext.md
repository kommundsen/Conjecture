# 0019. Imperative Composition via IGeneratorContext

**Date:** 2026-03-25
**Status:** Accepted

## Context

LINQ combinators (ADR-0018) handle simple transforms and dependent generation cleanly, but some strategies require imperative logic: loops to build collections of variable length, conditionals, mid-composition filtering, or assembling complex objects step by step. Python Hypothesis solves this with the `@composite` decorator and a `draw` callable. Conjecture.NET needs an equivalent that is idiomatic C#, readable, and preserves full automatic shrinking.

## Decision

Expose `Strategies.Compose<T>(Func<IGeneratorContext, T> factory)` where `IGeneratorContext` provides:

- `T Next<T>(Strategy<T> strategy)` — draw the next value from a sub-strategy, recording it in the shared `ConjectureData` buffer
- `void Assume(bool condition)` — soft rejection mid-composition; equivalent to `Assume.That()` at test level (see ADR-0020)

```csharp
var evenPairs = Strategies.Compose(gen =>
{
    var x = gen.Next(Integers(0, 100));
    var y = gen.Next(Integers(x, 200)); // depends on x
    gen.Assume(x + y < 250);
    return (x, y);
});
```

Shrinking is automatic: every `Next` call writes into the same `ConjectureData` byte buffer that the shrinker already operates on. No per-strategy shrink logic is required; reducing the buffer automatically produces simpler composed values.

## Consequences

- Imperative style is immediately readable to C# developers — no monad vocabulary required
- Loops, early returns, and conditional draws all work naturally
- Mid-composition `Assume` calls integrate cleanly with the filter budget (ADR-0020)
- `IGeneratorContext` must be non-serialisable and scoped to a single test execution; callers cannot cache or share it
- Async factory functions are not supported in the initial design — `Next` is synchronous; async property tests use a separate async overload of `Compose` if needed

## Alternatives Considered

- **Chained `SelectMany`** — correct and already supported (ADR-0018), but deeply nested chains become unreadable for more than two dependent draws
- **Async/await coroutines** — Python Hypothesis originally prototyped `draw` as `await`; abandoned because the coroutine machinery added significant complexity and the sequential imperative style achieves the same result without it
- **Expression tree builders** — compile-time safe and AOT-friendly, but cannot express runtime conditionals or loops; better suited to the `[Arbitrary]` source generator (ADR-0010) than to user-facing composition

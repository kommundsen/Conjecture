# 0018. Strategy Combinator Design

**Date:** 2026-03-25
**Status:** Accepted

## Context

Conjecture.NET needs a composable strategy API. Strategies must be transformable (map a value), filterable (reject unwanted values), and combinable (derive one strategy from the result of another). The design question is which surface to expose for this composition and how to ensure shrinking is correctly preserved through combinators.

## Decision

Use LINQ-style extension methods on `Strategy<T>`:

- `.Select<T, U>(Func<T, U> map)` — transforms generated values; shrinking is lifted automatically through the map
- `.Where(Func<T, bool> predicate)` — filters with a per-strategy rejection budget (see ADR-0020)
- `.SelectMany<T, U>(Func<T, Strategy<U>> bind)` — dependent generation; one strategy's output drives the next
- `.Zip<T, U>(Strategy<U> other)` — pairs two independent strategies into `(T, U)` tuples
- `.WithLabel(string label)` — annotates a strategy for diagnostics and failure output

`SelectMany` satisfies the monad laws, meaning shrinking propagates correctly through dependent chains without special-casing.

## Consequences

- .NET developers immediately recognise the combinators; query-expression syntax (`from x in strat select ...`) works out of the box
- Shrinking correctness is guaranteed structurally: because combinators preserve the underlying byte-buffer representation, the shrinker operates on buffers rather than values and needs no per-combinator shrink logic
- `.Where` couples tightly to the filter budget contract (ADR-0020); overuse degrades performance
- Adding new combinators is straightforward — any function `Strategy<T> → Strategy<U>` is expressible via `Select`/`SelectMany`

## Alternatives Considered

- **Custom fluent builder** (`.AndThen()`, `.Transform()`, `.DependOn()`) — more surface-discoverable but alien to .NET developers who already have a mental model for LINQ
- **Type classes / HKT emulation** — theoretically pure; C# 11+ static abstract members make it approachable, but the ergonomics are complex and the audience is general .NET developers, not FP specialists
- **Python-style `@composite` decorator only** — `Strategies.Compose<T>()` (ADR-0019) handles imperative composition, but is insufficient for simple one-liner transforms where `Select`/`SelectMany` are far cleaner

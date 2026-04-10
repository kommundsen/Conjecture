# 0041. Extension Member DSL

**Date:** 2026-04-10
**Status:** Accepted

## Context

Users frequently write `.Where(x => x > 0)` or `.Where(x => !string.IsNullOrEmpty(x))` to constrain strategies. These patterns are verbose, do not surface in IntelliSense, and are inefficient — `.Where()` uses rejection sampling, which degrades when the predicate rejects many values. A blessed set of named extension properties would improve discoverability, readability, and serve as a hook for future optimisation.

C# 14 introduces extension properties as a language feature, making this the right moment to define the initial API surface.

## Decision

- Ship a blessed set of extension properties in `Conjecture.Core`, appended to `StrategyExtensions.cs`. No separate package.
- Minimal initial set:
  - `Strategy<int>` → `.Positive`, `.Negative`, `.NonZero`
  - `Strategy<string>` → `.NonEmpty`
  - `Strategy<IList<T>>` → `.NonEmpty`
- Use terse property names (`.Positive`, not `.WherePositive()`) for IntelliSense discoverability.
- Implement blessed properties via `.Where()` internally. Document that tight-range constraints (e.g. values in `[1, 10]`) should use targeted strategies (`Generate.Int(1, 10)`) for efficiency.
- Add the `|` operator to `Strategy<T>` as sugar for `Generate.OneOf(a, b)`.
- Add Roslyn diagnostic **CJ0050**: detect common `.Where()` patterns that match a blessed extension property and suggest the named form.

## Consequences

- **Easier:** Discovery of common constraints via IntelliSense; cleaner test code; a natural on-ramp to the analyser.
- **Harder:** Each new blessed property extends the public API surface and requires a `PublicAPI.Unshipped.txt` entry; the minimal set must be chosen carefully to avoid premature commitment.
- Rejection-sampling cost remains for the current `.Where()` backing implementation. Users with hot inner loops should still reach for targeted strategies.

## Alternatives Considered

- **Fluent method style** (`.WherePositive()`): rejected — method syntax is less concise and breaks the terse DSL feel.
- **Separate `Conjecture.Extensions` package**: rejected — adds friction to the common case; the set is small enough to live in Core.
- **Expression-tree query compilation** (`IQueryable<T>`): deferred — synthesising targeted generators from predicates is the right long-term goal (tracked in #127) but requires significant engine work. The blessed-property approach ships value immediately and is compatible with a future compilation layer.

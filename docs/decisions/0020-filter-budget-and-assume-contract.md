# 0020. Filter Budget and Assume Contract

**Date:** 2026-03-25
**Status:** Accepted

## Context

Property-based testing frequently requires discarding generated values that fail a precondition. Conjecture.NET provides two mechanisms: strategy-level filtering via `.Where()` (ADR-0018) and test-level soft rejection via `Assume.That()`. Without constraints, a filter that rejects most values causes near-infinite loops and makes test suites hang silently. The design must allow normal, occasional filtering while detecting and surfacing pathological cases clearly.

## Decision

**Strategy-level (`.Where`):** Each `.Where` predicate is given a per-strategy rejection budget. The engine retries up to `MaxStrategyRejections` draws (default: **5**) for each accepted value. If the budget is exhausted, it raises `UnsatisfiedAssumptionException` internally — the same signal as a test-level assume failure — and the attempt counts against the test's overall unsatisfied budget.

**Test-level (`Assume.That`):** `Assume.That(condition)` is a soft rejection. It does not throw to the test runner; the engine catches it, discards the current test case, and generates a new one. This mirrors Python Hypothesis's `assume()`.

**Overall unsatisfied budget:** The engine tracks the ratio of unsatisfied to valid test cases. If the unsatisfied rate exceeds `MaxUnsatisfiedRatio` (default: **200 unsatisfied per 100 valid**), the test fails with:

```
Hypothesis gave up after N attempts: too many unsatisfied assumptions.
Consider loosening your filters or using a more targeted strategy.
```

Both thresholds are configurable via `ConjectureSettings` (ADR-0016):

```csharp
[Property(Settings = nameof(MySettings))]
// ConjectureSettings { MaxStrategyRejections = 20, MaxUnsatisfiedRatio = 5.0 }
```

## Consequences

- Tests with impossible or near-impossible filters fail fast with a clear message instead of hanging
- Normal use of `.Where()` for occasional filtering (e.g., even numbers, non-empty strings) is unaffected
- The Roslyn analyser (ADR-0023) can detect high-rejection patterns statically (CON101) and suggest targeted strategies instead
- The default budget of 5 retries is intentionally tight — it encourages users to write targeted strategies (e.g., `Integers(min: 0)`) rather than filtering broad ones
- `Assume.That` inside `Strategies.Compose` (ADR-0019) uses the same budget mechanism

## Alternatives Considered

- **No budget (infinite retry)** — simple to implement but makes tests silently hang when a filter rejects everything; unacceptable for a CI-facing library
- **Hard exception on first rejection** — too strict; occasional filtering is a legitimate and common pattern (e.g., filtering out edge cases for a specific sub-test)
- **Warning only, never fail** — masks bugs where a filter has a logic error and rejects 99% of values; the test appears to pass with far fewer valid cases than intended
- **Fixed global limit (not per-strategy)** — coarser; makes it hard to distinguish a legitimate test-level `Assume` from a broken `.Where` deep in a strategy chain

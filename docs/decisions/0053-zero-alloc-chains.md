# 0053. Zero-Alloc Chains: Scope and Allocation Budget

**Date:** 2026-04-19
**Status:** Accepted

## Context

PR #319 (StrategyCompositionBenchmarks) profiled per-iteration allocation for common strategy combinator chains. Three hotspots were identified:

- `SelectMany` inner-strategy factory: +32 B per call
- `Recursive` depth expansion: +268 B per call
- `Where` rejection `IRNode` records: +25 B per rejection retry

The `ConjectureData`/`IRNode` baseline of 384 B per iteration is the engine floor and is not attributable to combinators.

ADR-0018 (Strategy Combinator Design) established the class-based `Strategy<T>` hierarchy and internal sealed wrapper classes but did not define an allocation constraint. This ADR adds that constraint and records the scope decisions taken before implementation begins.

## Decision

**Scope reduction.** The `Strategy<T>` class hierarchy is preserved as-is. Wrappers remain `internal sealed class` types. Struct retrofits and a parallel `IStrategy<T>` interface were considered and explicitly deferred — they would require an ABI break and impose significant implementation complexity for uncertain gain beyond the three identified hotspots.

**`ref struct` skipped.** `ref struct` types do not compose with the class-based public `Strategy<T>` surface (they cannot be captured in closures or stored in fields of reference types). No profiled hotspot warrants the workaround complexity required to use them.

**Targeted fixes only.** Allocation reductions are applied only to the three hotspots above. Changes are validated against the allocation budget table below.

**Inlining policy.** `[MethodImpl(AggressiveInlining)]` is applied only to non-virtual leaf methods that show ≥5 % benchmark improvement in measured runs. It is never applied to public entry points or abstract/virtual overrides.

**Allocation budget** (baseline = `Integers_Baseline` from PR #319 benchmarks):

| Method | Budget |
|---|---|
| `Select_Single` | baseline ±1 B |
| `Where_Single` | baseline +≤16 B |
| `SelectMany_Single` | baseline +≤16 B |
| `Chain_ThreeOps` | baseline +≤32 B |
| `Recursive_Depth5` | baseline +≤128 B |

The CI allocation-regression gate (introduced in issue #80.4) fails on any >+10 % regression versus the PR #319 baseline.

## Consequences

- The combinator hotspots become tractable, focused changes with measurable targets.
- The `Strategy<T>` public API surface is unchanged — no consumer-visible breaking changes.
- Deferred struct/interface work remains possible in a future ADR if profiling warrants it.
- The CI gate prevents future regressions from silently eroding the gains.

## Alternatives Considered

**Struct-based `Strategy<T>`.** Would eliminate virtual dispatch and heap allocation for wrapper types. Rejected: requires a public ABI break and cannot carry a shrink tree without boxing. Deferred for a future design iteration.

**Parallel `IStrategy<T>` interface.** Would allow value-type implementations. Rejected for the same reasons as struct retrofits; the added indirection layer could introduce its own allocation overhead.

**`ref struct` combinators.** Would allow stack allocation of intermediate wrapper nodes. Rejected: incompatible with the class-based public surface and async/iterator scenarios. No hotspot justifies the workaround.

**Per-site object pooling.** Would reuse wrapper instances across calls. Rejected: introduces thread-safety complexity and lifetime ambiguity; gains are achievable through simpler targeted allocation elimination instead.

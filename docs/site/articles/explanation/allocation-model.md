# Understanding the allocation model

Property-based testing works by running a property hundreds or thousands of times. Each iteration generates fresh input, exercises the code under test, and checks the invariant. That tight loop means any per-iteration heap allocation multiplies fast — and the garbage collector eventually pays the bill.

## Why allocation matters more in property testing than in unit testing

A unit test runs once. A few extra allocations per test are invisible. A property test runs 100 times by default (and arbitrarily more with `[Property(Iterations = ...)]`). At 100 iterations, a 100 B per-iteration surplus accumulates to 10 KB of extra allocation per property. At 10,000 iterations — the kind of run you do when hardening a critical invariant — that same surplus becomes 1 MB of allocation that the GC has to reclaim.

The practical effects:
- **Throughput drops.** Each GC pause interrupts the iteration loop. More allocation means more pauses means slower test suites.
- **Pauses can mask timing-sensitive bugs.** If your property is sensitive to timing (channels, cancellation, ordering), GC pauses introduce jitter that can either hide a real race or produce a spurious failure.
- **Allocation noise obscures profiling.** If every iteration allocates several kilobytes, the signal from a real allocation regression is buried.

None of this is unique to Conjecture — it applies to any high-iteration testing framework. It is just more visible here because the iteration count is the point.

## The engine floor

Every `Generate` call has an unavoidable allocation floor: `ConjectureData` (the IR buffer and metadata) plus the `IRNode` records it writes as it reads from the byte stream. Profiling with `[MemoryDiagnoser]` in BenchmarkDotNet established this floor at **384 B per iteration** for the simplest possible strategy — a bare integer generation (`Integers_Baseline`):

| Method            | Mean      | Allocated |
|-------------------|-----------|-----------|
| Integers_Baseline |  60.22 ns |     384 B |
| Select_Single     |  57.46 ns |     384 B |
| Where_Single      |  82.27 ns |     409 B |
| SelectMany_Single |  75.98 ns |     416 B |
| Chain_ThreeOps    |  97.92 ns |     452 B |
| Recursive_Depth5  | 118.29 ns |     652 B |

`Select_Single` comes in at the same 384 B as the baseline — the `Select` wrapper adds no heap allocation of its own. `Where_Single`, `SelectMany_Single`, and the chained pipeline each add a small delta. `Recursive_Depth5` adds the most: 268 B on top of the floor.

The 384 B engine floor is deliberately out of scope for the optimization work described below. It is not attributable to combinators, and eliminating it would require restructuring the IR at a depth that would touch the entire engine.

## Allocation budgets and the CI gate

Before any optimization work began, [ADR-0053](../../decisions/0053-zero-alloc-chains.md) formalized a per-method allocation budget — a cap on how many bytes above the `Integers_Baseline` each combinator may allocate:

| Method | Budget above baseline |
|---|---|
| `Select_Single` | ±1 B |
| `Where_Single` | +≤16 B |
| `SelectMany_Single` | +≤16 B |
| `Chain_ThreeOps` | +≤32 B |
| `Recursive_Depth5` | +≤128 B |

The budgets were chosen to match the allocation levels achievable by the targeted fixes, leaving a small margin for future combinators layered on top without immediately breaching the cap.

The CI gate is implemented in `AllocationBudgetValidator` (`Conjecture.Benchmarks`). It accepts the measured baseline, and a dictionary of `(actual, budget)` pairs per method, and returns a list of failures. A method exceeds the gate when its actual allocation exceeds `baseline + budget`. Any >+10 % regression against the PR #319 numbers also triggers the gate, so a future change that shifts the baseline cannot silently move all the budgets upward.

## The three hotspot fixes

Profiling identified three combinators that contributed allocation beyond what the engine floor alone explains:

### SelectMany: per-`Generate` inner-strategy construction

`SelectManyStrategy<TSource,TCollection,TResult>.Generate` called the `collectionSelector` delegate on each iteration, which **constructed a new `Strategy<TCollection>` instance every call** — +32 B over baseline. The fix, introduced as `SelectManyDirectStrategy`, fuses the inner generation into the outer call: the inner strategy is never materialised as a heap object. `SelectMany_Single` now sits at the `baseline + ≤16 B` budget.

### RecursiveStrategy: depth expansion on every call

`RecursiveStrategy<T>.Generate` rebuilt a fresh `DepthLimitedStrategy<T>` chain on each call. Five levels deep meant five allocations, each capturing a closure over the level above — roughly 53 B per level, 268 B total. The fix uses lazy memoised expansion: the chain is built once on first use and reused on every subsequent `Generate`. `Recursive_Depth5` now comes in at `baseline + ≤128 B`.

### WhereStrategy: orphaned IRNode records on rejection

`WhereStrategy<T>.Generate` runs a rejection loop: it generates a candidate, checks the predicate, and retries up to 200 times on failure. Each rejected candidate appended an `IRNode` record to `ConjectureData.nodes`, and the list's backing array could grow — +25 B per average rejection. The fix uses speculative-record rollback: before entering the loop, it snapshots the node count; on rejection, it truncates back to the snapshot so only the accepted candidate leaves a record. `Where_Single` now comes in at `baseline + ≤16 B`, and the shrinker gets cleaner IR as a bonus.

## What was not done — and why

Three more aggressive approaches were evaluated and explicitly deferred in ADR-0053:

**Struct-based `Strategy<T>`.** Replacing the abstract class with a struct or a value-type hierarchy would eliminate virtual dispatch and heap allocation for wrapper types entirely. It was rejected: the public ABI would break, and a struct cannot carry a mutable shrink tree without boxing — which would immediately erase any allocation gain at the exact moment it matters most (during shrinking).

**Parallel `IStrategy<T>` interface.** A new interface would allow internal struct implementations alongside the public class hierarchy. Same rejection reasons: ABI break, plus the risk of introducing new allocation on the interface indirection path.

**`ref struct` combinators.** Stack-allocated `ref struct` wrappers would give the JIT the information it needs to elide heap allocation entirely for short-lived strategy chains. Rejected: `ref struct` cannot be captured in closures or stored as fields of reference types, which rules out composition with the class-based `Strategy<T>` surface, and cannot be used across async or iterator boundaries. No profiled hotspot justified the workaround.

The practical conclusion: the three targeted fixes brought every budget line within range without touching the public API or the class hierarchy.

## Further reading

- [ADR-0018: Strategy Combinator Design](../../decisions/0018-strategy-combinator-design.md) — the original design of `Strategy<T>` and internal sealed wrappers
- [ADR-0053: Zero-Alloc Chains](../../decisions/0053-zero-alloc-chains.md) — the scope decision, allocation budgets, and deferred alternatives
- [Understanding shrinking](shrinking.md) — how the byte-buffer IR that the engine builds feeds the shrinker

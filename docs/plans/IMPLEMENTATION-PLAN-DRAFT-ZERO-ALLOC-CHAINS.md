# Draft: Zero-Allocation Strategy Chains

## Motivation

.NET 10's JIT brings expanded escape analysis: stack-allocated delegates, small arrays (value and reference types), and struct field references. Strategy composition chains like `.Select().Where().SelectMany()` create intermediate objects (closures, strategy wrappers) that the JIT may now stack-allocate. This draft covers both "free" wins from the JIT and explicit optimizations we can make.

## .NET Advantage

.NET 10's expanded escape analysis (stack-allocated delegates, small arrays, struct field tracking) means the JIT can automatically eliminate heap allocations in strategy composition chains. Combined with `Span<T>`, `ArrayPool<T>`, and aggressive inlining, Conjecture can achieve near-zero GC pressure during high-iteration property runs without requiring users to change their code.

## Key Ideas

### JIT Freebies (.NET 10 automatic)
- Delegate closures in `.Select(x => x + 1)` may be stack-allocated if they don't escape
- Small arrays created during collection strategy generation may be stack-allocated
- Strategy wrapper structs referenced by local variables won't be marked as escaping

### Explicit Optimizations
- **Struct-based strategy wrappers**: Replace `SelectStrategy<T, U> : Strategy<U>` (class) with a struct-based approach where the JIT can inline and promote
- **Pooled strategy pipelines**: Reuse strategy objects across iterations via `ObjectPool<T>`
- **`ref struct` composition**: For short-lived strategy chains that don't cross async boundaries
- **Inlining hints**: `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot composition paths

### Benchmark-Driven Approach
1. Baseline: measure allocations per property execution for common strategy patterns
2. Profile: identify which strategy wrappers dominate allocation
3. Optimize: apply struct/pooling/inlining selectively
4. Verify: ensure no allocation regression via `[MemoryDiagnoser]` in BenchmarkDotNet

## Design Decisions to Make

1. Can strategy wrappers be changed from classes to structs without breaking the public API?
   - `Strategy<T>` is a class — struct wrappers would need a different composition pattern
2. Is `ref struct` viable for any strategy types? (Can't be used in async, can't implement interfaces)
3. How to maintain debuggability when strategies are aggressively inlined?
4. Allocation budget: what's an acceptable allocation-per-example target?

## Scope Estimate

Small-Medium. JIT freebies are automatic. Explicit struct/pooling changes require API analysis. ~1-2 cycles.

## Dependencies

- `Conjecture.Benchmarks` with `[MemoryDiagnoser]`
- .NET 10 JIT (escape analysis improvements)
- Existing strategy class hierarchy

## Open Questions

- What percentage of allocations come from strategy composition vs the byte buffer vs test framework overhead?
- Does `Strategy<T>` being abstract class prevent JIT devirtualization of `.Generate()` calls?
- Would a `IStrategy<T>` interface with struct implementations be worth the API complexity?
- How do allocation profiles differ between simple strategies (e.g., `Integers<int>()`) and complex compositions (e.g., `Recursive<T>`)?

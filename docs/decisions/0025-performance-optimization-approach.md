# 0025. Performance Optimization Approach

**Date:** 2026-03-25
**Status:** Accepted

## Context

Conjecture.NET's hot path — `ConjectureData` buffer reads/writes, `Block` struct tracking, and shrink pass execution — runs thousands of times per property test. Several micro-optimisation techniques are available: SIMD zero-block detection, frozen block arrays, tiered shrink scheduling, parallel generation, UTF-8 string internals, and converting `ConjectureData` to a `ref struct`. Applied speculatively, these add implementation complexity and increase the risk of correctness bugs before the design has stabilised. Applied too late, silent performance regressions compound across releases.

## Decision

**Profile-driven optimisation only.** No speculative micro-optimisations are applied during Phase 0–2. Candidate techniques are documented here and reserved for Phase 4, gated behind profiling evidence.

**Candidate techniques and their preconditions:**

| Technique | Precondition to implement |
|-----------|--------------------------|
| SIMD zero-block detection (`Vector256<byte>.IsAllZeros()`) | Zero-block pass appears as a hotspot in BenchmarkDotNet CPU profiles |
| Frozen block list (`List<Block>` → `Block[]` post-generation) | Cache-miss pressure visible in profiler; low-risk, implement early in Phase 4 |
| Tiered shrink scheduling (priority 0–5, cheap-to-fixpoint first) | **Implement from the start** — already part of ADR-0021 design; not speculative |
| Parallel generation (`IThreadPoolWorkItem` for concurrent buffer exploration) | Single-threaded generation is a measured bottleneck; requires careful isolation (ADR-0017) |
| UTF-8 string internals (`ReadOnlyMemory<byte>` during shrinking) | Allocation pressure visible in string-heavy benchmark profiles |
| `ConjectureData` as `ref struct` | Significant refactor; heap allocation of `ConjectureData` is a measured hotspot; high risk |

**Decision gate:** an optimisation is implemented only if:
1. A BenchmarkDotNet profile shows measurable impact in a realistic test scenario (not a microbenchmark designed to show improvement), **and**
2. The implementation does not increase public API complexity or introduce correctness risk.

**Performance regression gating:** a BenchmarkDotNet benchmark suite runs in CI. Any benchmark that regresses more than **15% vs the stored baseline** (i.e., ratio > 1.15) triggers a CI alert. Baselines are committed to source control alongside the benchmarks.

## Consequences

- The codebase stays simple and correct during the critical Phase 0–2 period when the API and engine design are still evolving
- Tiered shrink scheduling (the highest-value structural change) ships from the start as it is an architectural decision, not a micro-optimisation
- Phase 4 work is scoped by data rather than intuition — candidate techniques that don't show up in profiles are never implemented
- The 15% CI threshold catches accidental regressions introduced by new features without requiring manual benchmark audits
- BenchmarkDotNet baselines must be updated intentionally when a deliberate performance trade-off is made (e.g., adding a new block metadata field)

## Alternatives Considered

- **Speculative upfront optimisation** — applying all candidate techniques in Phase 0–1 adds significant implementation complexity before the design is stable, increases the chance of subtle correctness bugs in the hot path, and makes the codebase harder to reason about for contributors; premature optimisation in a library used for testing is especially risky
- **No performance tracking** — without CI regression gating, performance degrades silently as features accumulate; property-based testing libraries are often abandoned when they become "too slow to run on every PR", making this a long-term adoption risk

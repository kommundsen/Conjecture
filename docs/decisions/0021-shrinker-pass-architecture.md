# 0021. Shrinker Pass Architecture

**Date:** 2026-03-25
**Status:** Accepted

## Context

When Conjecture.NET finds a failing test case it must reduce the counterexample to the simplest possible form before reporting it. Shrinking quality is the primary differentiator between Hypothesis and other .NET property-based testing libraries — the difference between "falsified with `[0, 1, -1]`" and "falsified with a list of 847 random integers" is what makes failures actionable. The key design question is whether to shrink at the *value* level (each strategy knows how to shrink its own output) or at the *buffer* level (the engine shrinks the raw byte representation, independent of type).

## Decision

Use block-level, type-independent buffer shrinking, inheriting the approach from Python's Conjecture engine (ADR-0008). The shrinker operates on the raw `ConjectureData` byte buffer; it never inspects typed values. Shrinking is defined as finding a lexicographically smaller valid buffer that still causes the test to fail.

**Pass tiers:**

*Phase 0 — basic passes (always present from first release):*
1. **Zero blocks** — set a block's bytes to zero
2. **Delete blocks** — remove a block entirely and re-run
3. **Lexicographic minimize** — reduce individual byte values toward zero
4. **Integer reduction** — treat a block as a little-endian integer and binary-search toward a smaller value

*Phase 2 — advanced passes (added for shrink quality):*
5. **Adaptive passes** — track which blocks recently produced progress; bias effort toward those blocks
6. **Block swapping** — try reordering adjacent blocks to find a lexicographically smaller arrangement
7. **Redistribution** — move magnitude between adjacent integer-typed blocks (e.g., reduce one while increasing another, net-smaller)
8. **Interval deletion** — delete contiguous runs of blocks in one operation
9. **Float simplification** — recognise IEEE 754 float blocks and apply float-specific reductions (NaN → 0, ±Inf → max finite, etc.)
10. **String-aware passes** — operate on UTF-8-encoded string blocks to prefer shorter, ASCII-only strings

**Scheduling:** passes are assigned a priority (0 = cheapest, 5 = most expensive). Cheap passes run to fixpoint before expensive passes begin. This mirrors Python Hypothesis's tiered scheduling and avoids spending time on expensive structural mutations when a simple zero-pass would suffice.

**Mutation strategy:** ~70–80% of passes use in-place mutation with rollback via `ref struct ShrinkMutation` (saves/restores only the affected byte region, `stackalloc` for regions ≤128 bytes). Full buffer cloning is reserved for structural mutations (block deletion, interval removal) where the buffer length changes.

## Consequences

- Strategy authors write zero shrink logic — combinators, `Compose`, and the source generator all shrink automatically
- Shrink quality is uniform across all types; a custom `Strategy<MyRecord>` shrinks as well as built-in strategies
- The shrinker can be improved centrally without touching any strategy code
- Phase 0 ships a usable shrinker immediately; Phase 2 passes are additive and can be introduced incrementally
- The in-place mutation model requires `ConjectureData` to expose a mutable buffer view; this is an internal API constraint
- Float and string-aware passes require the engine to track block *kind* metadata alongside byte offsets — a modest overhead in the `Block` struct

## Alternatives Considered

- **Value-level shrinking** (`Strategy<T>` implements `IShrinkable<T>`) — used by FsCheck and early Hedgehog; every strategy author must write correct shrink logic, combinators multiply the burden, and shrink quality varies widely across the ecosystem. Rejected because it undermines the library's quality guarantee.
- **Integrated generation + shrinking (shrink trees)** — used by Hedgehog and Hypothesis's earliest prototype; elegant in theory, but requires strategies to enumerate an explicit shrink tree, which is incompatible with the Conjecture byte-buffer model and makes imperative composition (ADR-0019) significantly harder to implement correctly.

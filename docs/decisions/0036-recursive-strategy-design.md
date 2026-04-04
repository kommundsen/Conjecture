# 0036. Recursive Strategy Design

**Date:** 2026-04-03
**Status:** Accepted

## Context

Phase 5 adds recursive/tree-shaped strategies — `Generate.Recursive<T>()` — for generating expression trees, JSON, nested lists, and other recursive data structures with bounded depth control. This was deferred from Phase 4.

Many real-world property tests involve recursive data: expression trees for compilers, JSON values for serializers, nested collections for data processing. Without a built-in recursive strategy, users must manually implement depth tracking and base-case fallback, which is error-prone and produces poor shrinking.

## Decision

### Public API

`Generate.Recursive<T>(Strategy<T> baseCase, Func<Strategy<T>, Strategy<T>> recursive, int maxDepth = 5)` — user provides a base case strategy and a function that receives a "self" strategy and returns a combined strategy (typically using `Generate.OneOf`).

The separation of base case from recursive case is explicit in the API signature, making it clear which alternatives are leaves vs recursive branches.

### Internal Implementation

`RecursiveStrategy<T>` internally wraps a `DepthLimitedStrategy<T>` that tracks remaining depth:

- The top-level `Generate` draws an integer `[0, maxDepth]` from the IR stream to select the initial depth budget.
- At each recursive use of "self", the depth counter decrements.
- When depth reaches 0, "self" substitutes `baseCase` instead of the recursive strategy.
- This ensures termination and bounded tree size.

### Depth Tracking via IR Draws

Each depth decision is recorded as an integer IR node draw. This makes depth shrinkable by the existing `IntegerReductionPass` — the shrinker naturally reduces depth values toward 0, producing shallower (simpler) trees. No new shrink pass is needed.

### NativeAOT Safety

No reflection — just generic lambda composition. `RecursiveStrategy<T>` is `internal sealed`, exposed only via the `Generate.Recursive<T>` factory.

## Consequences

- Enables generation of recursive data structures (ASTs, JSON, nested collections) with automatic depth bounding
- Shrinking naturally produces shallower trees via existing integer reduction passes
- No new shrink pass needed
- Base/recursive split in the API makes termination guarantees clear
- `maxDepth` parameter gives users explicit control over maximum tree depth
- Composable with all existing combinators (`Select`, `Where`, `SelectMany`, `Zip`)
- NativeAOT-safe: no reflection involved

## Alternatives Considered

1. **Single callback `Func<Strategy<T>, Strategy<T>>` with automatic base case detection** — harder to guarantee termination; unclear which `OneOf` alternatives are base cases. Rejected for explicit API clarity.
2. **Depth tracked via ambient mutable state** — fragile, not thread-safe, not shrinkable. Rejected.
3. **New `IRNodeKind.RecursionDepth`** — unnecessary; plain integer draws work and are already handled by existing shrinker passes. Rejected.
4. **Fuel-based approach (draw a boolean "continue?" at each level)** — shrinks less predictably; boolean draws don't reduce depth as cleanly as integer draws. Rejected.

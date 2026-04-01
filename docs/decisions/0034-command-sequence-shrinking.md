# 0034. Command Sequence Shrinking

**Date:** 2026-04-01
**Status:** Accepted

## Context

Stateful testing (ADR-0015) generates sequences of commands against an `IStateMachine<TState, TCommand>` implementation. When the engine finds a failing sequence it must shrink it — finding the shortest sequence that still violates the invariant. Standard IR-node shrinking passes (ZeroBlocks, DeleteBlocks, LexMin, etc.) reduce individual drawn values but have no concept of command boundaries. A dedicated pass is needed that understands the command structure and can drop whole commands from the sequence.

The pass must also handle state-dependent command availability: after dropping a command, subsequent commands may become inapplicable (because `Commands(state)` no longer offers that command type), changing the execution path and potentially losing the invariant violation.

## Decision

`StateMachineStrategy` marks command-start positions in the IR stream by drawing a zero-value sentinel integer with kind `IRNodeKind.CommandStart` (new enum value = 7) immediately before each command's draw calls. The sentinel value is always 0 and is never used for generation — it exists solely as a structural boundary marker. This requires adding `CommandStart = 7` to the `IRNodeKind` enum.

`CommandSequenceShrinkPass` implements `IShrinkPass` (`ValueTask<bool> TryReduce(ShrinkState state)`) and is registered in `Shrinker.PassTiers[0]` (tier 0, alongside `ZeroBlocksPass`, `DeleteBlocksPass`, `IntervalDeletionPass`). It scans `ShrinkState.Nodes` for `CommandStart` sentinels to build a command span index, then runs three sub-passes in order:

1. **Truncate-from-end:** Drop the last command's node span (from its `CommandStart` sentinel to just before the next sentinel, or end of list).
2. **Binary-halve:** Drop the second half of command spans.
3. **Delete-one-at-a-time:** Try removing each command span individually in order (mirrors `DeleteBlocksPass` but at command granularity).

After constructing a candidate node list for each attempt, interestingness is delegated to `state.TryUpdate` (existing machinery). No separate forward-simulation of state-dependent command applicability is performed: if a candidate sequence produces a non-interesting run — e.g., because removing a command makes a later one inapplicable, changing the execution path so the invariant no longer fires — `TryUpdate` returns false and the candidate is discarded.

Non-stateful tests are unaffected: the pass returns false immediately when no `CommandStart` nodes are present in `ShrinkState.Nodes`.

## Consequences

- Command boundaries are self-describing in the IR stream; no auxiliary data structure needs to survive across `TryUpdate` calls (which replace the node list entirely).
- The approach reuses `state.TryUpdate` for interestingness checking, so command-sequence shrinking benefits from the same interestingness semantics as all other passes without additional machinery.
- Tier-0 placement means command-sequence deletion runs before expensive lexicographic passes, producing shorter sequences early and reducing the search space for later passes.
- Adding `CommandStart = 7` to `IRNodeKind` is a non-breaking internal change; no public API is affected and existing IR streams contain no `CommandStart` nodes so all existing shrink passes skip them by kind.
- The zero-value sentinel adds one IR node per command step; for a 50-step sequence this is 50 extra nodes, a negligible overhead compared to the nodes drawn by the command strategy itself.

## Alternatives Considered

- **Auxiliary `CommandRecord` list stored outside IR nodes:** Cleaner separation of concerns, but the record becomes stale after each `TryUpdate` call (which replaces the entire node list), requiring costly re-synchronisation or re-execution to rebuild the index. Rejected in favour of self-describing sentinel nodes.
- **`IRNodeKind.CommandBoundary` between commands rather than `CommandStart` before each:** Equivalent structural information but slightly harder to slice spans — boundary nodes sit between commands, so finding the start of a span requires tracking both the list start and each boundary position, whereas `CommandStart` sentinels directly mark span beginnings.
- **Shrinking at the length-integer level only:** The sequence length integer drawn first by `StateMachineStrategy` is already reduced by the existing `IntegerReductionPass` and `LexMinimizePass`, but these can only truncate the sequence from the end. `CommandSequenceShrinkPass` adds the ability to delete commands from arbitrary positions, which is necessary to find minimal sequences where the violation depends on a specific ordering of early commands.

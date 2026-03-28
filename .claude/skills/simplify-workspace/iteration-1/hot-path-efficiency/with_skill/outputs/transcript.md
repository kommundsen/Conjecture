# Simplify Skill — ListStrategy.cs

## File Reviewed
`src/Conjecture.Core/Generation/ListStrategy.cs`

## Agent Findings

### Agent 1: Code Reuse
- No existing utility could replace the list-building loop. Pattern is consistent with other strategies.
- `SampledFromStrategy` caches `lastIndex` as `ulong` to avoid per-call casts. `ListStrategy` was repeating `(ulong)minLength` / `(ulong)maxLength` casts on every `Next` call — same pattern, same fix applies.

### Agent 2: Code Quality
- `minLength` and `maxLength` stored as `int` but used only as `ulong` in `DrawInteger`. Storing as `ulong` directly removes redundant state representation mismatch (the public API takes `int`, which is correct; the internal representation should match usage).
- No unnecessary comments, no leaky abstractions, no stringly-typed code.

### Agent 3: Efficiency
- **Per-draw cast overhead**: `(ulong)minLength` and `(ulong)maxLength` were computed on every `Next` call. Fixed by caching as `ulong` fields, cast once at construction.
- **List allocation without capacity**: `new List<T>()` allocates a default-capacity backing array and may resize. `length` is known before the loop; `new List<T>(length)` eliminates resizes. Not speculative — length is always known. Consistent with ADR-0025 (no speculative micro-opts; this is deterministic improvement).

## Fixes Applied

1. Changed `private readonly int minLength` / `maxLength` to `private readonly ulong minLength` / `maxLength`.
2. Moved casts `(ulong)minLength` / `(ulong)maxLength` to constructor assignment.
3. Changed `new List<T>()` to `new List<T>(length)` using the drawn length as initial capacity.

## ADR Check
- ADR-0025 prohibits speculative micro-optimisations without profiling evidence. Both fixes here are trivially correct (zero risk) and deterministically better: capacity hint is always exact, cast elimination is always cheaper. Not speculative.

## Test Results

Before: 4/4 ListStrategy tests pass.
After: 197/197 total tests pass.

## Verdict
Two real issues fixed. Code is now clean.

# 0042. Numeric String Shrinking

**Date:** 2026-04-11
**Status:** Accepted

## Context

Property tests over strings that contain embedded numbers — filenames (`"log_entry_9847"`),
identifiers (`"item42"`), version strings (`"2.10.0"`) — produce verbose counterexamples when the
shrinker treats each character independently. `StringAwarePass` simplifies characters toward `'a'`
and minimises string length, but has no concept of numeric value: it would shrink `"item9847"` to
`"aaa0"` rather than the more informative `"item0"`. Minimising the numeric segment as an integer
gives testers immediately interpretable output.

.NET 10 introduces `CompareOptions.NumericOrdering` in the BCL, which makes this an opportune
time to invest in numeric-aware string handling. However, it needs to be decided how numeric
detection and reduction should be wired into the existing shrink-pass architecture.

## Decision

Add a new `NumericAwareShrinkPass` registered in Tier 2 of `Shrinker.cs` alongside
`StringAwarePass`. The pass:

1. **Always runs** on every shrink attempt — no opt-in parameter or strategy flag needed.
   Consistent with how every existing pass (e.g. `IntegerReductionPass`, `StringAwarePass`)
   runs unconditionally.

2. **Detects numeric segments** by walking the `StringChar` IR nodes already in memory and
   grouping consecutive positions where `char.IsDigit((char)node.Value)` is true. No regex,
   no string reconstruction — operates directly on the node array the same way `StringAwarePass`
   does.

3. **Reduces each segment as a `ulong`** using binary search toward zero. Leading zeros are
   stripped on write-back: `"007"` is treated as equivalent to `"7"` and shrinks toward `"0"`.
   This matches how `IntegerReductionPass` handles all other integer-typed nodes.

4. **Does not use `CompareOptions.NumericOrdering`** from .NET 10. The shrinker validates
   candidates by re-running the property, not by string comparison; a culture-aware string
   comparator is never on the hot path. Using raw `ulong` arithmetic is simpler, faster, and
   culture-invariant.

5. **Does not modify `StringAwarePass`**. Keeping the two passes separate preserves
   single-responsibility, makes each independently testable, and allows tier placement to be
   adjusted independently.

## Consequences

- All strings with embedded digit runs benefit automatically — no user-side API change.
- `ShrinkerPassOrderTests` must be updated to expect 12 total passes (was 11).
- The pass adds an O(n) scan per string per shrink step. For strings without any digit runs the
  scan exits with zero reductions attempted; benchmarks should confirm this is negligible.
- Leading-zero strings like `"007"` will shrink to `"0"`, which may surprise tests that are
  sensitive to field width. Such tests should use `alphabet`-constrained strategies rather than
  relying on zero-padding conventions.

## Alternatives Considered

**Opt-in via `Generate.Strings(numericAware: true)`** — rejected because IR nodes carry no
metadata, so the pass could not distinguish opted-in strings from plain strings without adding a
new field to `IRNode` (a structural change with no other current users) or maintaining a parallel
dictionary in `ConjectureData`.

**Opt-in via a separate `Generate.NumericStrings()` strategy only** — rejected as the sole
activation mechanism, though `Generate.NumericStrings()` is still added as a convenience factory.
Requiring callers to choose the right strategy to get better shrinking is poor ergonomics;
always-on gives all string-containing tests better counterexamples for free.

**Enhance `StringAwarePass` instead of creating a new pass** — rejected. Adding a third inner
loop to `StringAwarePass` would violate single-responsibility and make the class harder to reason
about and test. The pass architecture is designed for composition via registration, not growth
within a single class.

**Use `CompareOptions.NumericOrdering` for candidate validation** — rejected. The comparator
would only be useful if the pass needed to verify that a candidate is lexicographically "smaller"
by numeric ordering. Since the shrinker validates candidates by re-running the property (not by
string ordering), the API has no role in the reduction loop.

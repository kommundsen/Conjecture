# Draft: Numeric-Ordering-Aware String Shrinking

## Motivation

.NET 10 introduces `CompareOptions.NumericOrdering` which treats embedded numbers in strings numerically rather than lexicographically (`"2" < "10"`, `"02" == "2"`). This can improve string shrinking for strings containing numbers — filenames, identifiers, version strings, log entries — producing more intuitive minimal counterexamples.

## .NET Advantage

.NET 10 introduces `CompareOptions.NumericOrdering` in the base class library, providing culture-aware numeric string comparison natively. This API enables Conjecture's shrinker to understand that `"2" < "10"` numerically, producing more intuitive minimal counterexamples for strings containing embedded numbers — something that requires no external library or custom comparison logic.

## Key Ideas

### Numeric-Aware String Shrink Pass
- New `IShrinkPass` that identifies numeric segments within strings
- Shrinks numeric segments as integers (e.g., `"item10"` → `"item2"` → `"item1"` → `"item0"`)
- Preserves non-numeric segments unchanged
- Uses `CompareOptions.NumericOrdering` for comparison during shrink validation

### Strategy-Level Integration
- `Generate.Strings()` gains an optional `numericAware: true` parameter
- When enabled, the string strategy labels numeric segments for the shrinker
- `Generate.VersionStrings()` — built-in strategy for semver-like strings
- `Generate.Identifiers()` — strategy for `"prefix123"` patterns

### Shrinking Behavior
```
Original:     "log_entry_9847"
Shrink step 1: "log_entry_1"     (numeric segment minimized)
Shrink step 2: "a_a_1"           (string segments minimized)
Shrink step 3: "a1"              (structure minimized)
```

## Design Decisions to Make

1. Should numeric-aware shrinking be opt-in or default?
2. How to detect numeric segments: regex, `char.IsDigit()` scan, or label during generation?
3. Does this belong as a new `IShrinkPass` or an enhancement to the existing `StringAwareShrinkPass`?
4. How to handle leading zeros: is `"007"` equivalent to `"7"` for shrinking purposes?
5. Culture sensitivity: `CompareOptions.NumericOrdering` respects culture — should shrinking be culture-invariant?

## Scope Estimate

Small. Focused enhancement to an existing shrink pass. ~1 cycle.

## Dependencies

- .NET 10 `CompareOptions.NumericOrdering` API
- Existing `StringAwareShrinkPass` in `Conjecture.Core`
- `ConjectureData` byte buffer (numeric segments encoded as integer draws)

## Open Questions

- How common are numeric-embedded strings in real property test failures? (Need examples from real-world usage)
- Does `NumericOrdering` add measurable overhead to string comparison in the shrink loop?
- Should we also provide a `Generate.NumericStrings()` that produces strings with guaranteed numeric segments?

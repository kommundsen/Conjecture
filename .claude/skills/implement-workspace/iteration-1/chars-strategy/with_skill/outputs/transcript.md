# Implementation Transcript: Gen.Chars()

## Initial Test Run

Ran `dotnet test src/ --filter "FullyQualifiedName~CharStrategy"`.

Build errors:
- `error CS0117: 'Gen' does not contain a definition for 'Chars'` (3 occurrences, all in CharStrategyTests.cs)

## Files Read

- `.claude/skills/implement/SKILL.md` — skill instructions
- `src/Conjecture.Core/Gen.cs` — factory class to extend
- `src/Conjecture.Core/Generation/SampledFromStrategy.cs` — pattern for sampling from a collection
- `src/Conjecture.Core/PublicAPI.Unshipped.txt` — public API tracking

## Design Decisions

`Gen.Chars()` delegates to the existing `SampledFrom` method with a pre-built `char[]` for printable ASCII (0x20–0x7E). `Gen.Chars(char[] alphabet)` delegates similarly. No new strategy class needed — `SampledFromStrategy<char>` handles both cases perfectly. Kept `PrintableAscii` as a private static readonly field to avoid re-allocating on every call.

Hit a naming rule violation (IDE1006) on first attempt using `printableAscii` — corrected to `PrintableAscii` (private static fields must start with uppercase in this project's style config).

## Files Modified

- `src/Conjecture.Core/Gen.cs` — added `PrintableAscii` field, `Chars()`, and `Chars(char[])` methods
- `src/Conjecture.Core/PublicAPI.Unshipped.txt` — added two new public API signatures

## Final Test Results

```
dotnet test src/ --filter "FullyQualifiedName~CharStrategy"

Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 174 ms

dotnet test src/

Passed!  - Failed: 0, Passed: 196, Skipped: 0, Total: 196, Duration: 93 ms
```

All 3 new tests pass; 0 regressions in 196 total.

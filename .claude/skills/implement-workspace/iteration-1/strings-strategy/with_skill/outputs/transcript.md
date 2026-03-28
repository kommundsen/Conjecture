# Implement StringStrategy — Transcript

## Initial failures

```
CS0117: 'Gen' does not contain a definition for 'Strings'
```
4 compiler errors, one per test.

## Files created / modified

### Created
- `src/Conjecture.Core/Generation/StringStrategy.cs` — internal sealed class extending `Strategy<string>`
- `src/Conjecture.Tests/Strategies/StringStrategyTests.cs` — 4 xUnit tests (provided)

### Modified
- `src/Conjecture.Core/Gen.cs` — added `Gen.Strings(char[]? alphabet, int minLength, int maxLength)`
- `src/Conjecture.Core/PublicAPI.Unshipped.txt` — declared the new public method signature

## Design decisions

- Default alphabet: printable ASCII (chars 32–126, 95 characters) — wide enough to be useful without requiring unicode awareness.
- Default minLength=0, maxLength=20 — matches common Hypothesis defaults; tests demanded specific bounds work, not a particular default.
- `DrawInteger` used for both length and char index — consistent with existing strategies.
- IDE0011 (braces on for) enforced by the project analyzer; fixed immediately.

## Build issues encountered

1. `IDE0011` — missing braces on `for` body — fixed by adding braces.

## Final test results

### Target tests
```
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4
```

### Full suite
```
Passed!  - Failed: 0, Passed: 197, Skipped: 0, Total: 197
```

No regressions.

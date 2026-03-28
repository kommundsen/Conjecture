# Simplify Skill — duplicate-alphabet eval

## Target file
`src/Conjecture.Core/Gen.cs`

## Issues found

### Agent 1: Code Reuse
- `Enumerable.Range(32, 95).Select(i => (char)i).ToArray()` duplicated:
  - inline in `Gen.Chars()` (Gen.cs line 49–50)
  - inline in `StringStrategy` constructor as the `null` fallback (StringStrategy.cs)

### Agent 2: Code Quality
- Copy-paste with slight variation: same LINQ expression in two files, both defining "printable ASCII"
- `Strings()` passes `null` through to `StringStrategy` which then has its own duplicate expression — both places define the same concept independently

### Agent 3: Efficiency
- `Gen.Chars()` allocated a new `char[95]` array on every call due to inline LINQ

## Fixes applied

1. Extracted `private static readonly char[] PrintableAscii` to `Gen.cs`
2. `Chars()` now references `PrintableAscii` directly — no per-call allocation
3. `Strings()` resolves `null` at the `Gen` layer (`alphabet ?? PrintableAscii`) and passes the concrete array to `StringStrategy`, so `StringStrategy`'s duplicate fallback path is never reached via the public API

## Skipped / false positives
- `StringStrategy.cs` still contains its own fallback `Enumerable.Range(32, 95)...` expression. Skill scope is `Gen.cs` only; left in place. It is now dead code via the public API but remains correct for direct construction.

## Test results

### Before fix (green baseline)
`dotnet test src/ --filter "FullyQualifiedName~CharStrategy|FullyQualifiedName~StringStrategy"`
→ Passed: 4 / 4

### After fix
`dotnet test src/`
→ Passed: 197 / 197

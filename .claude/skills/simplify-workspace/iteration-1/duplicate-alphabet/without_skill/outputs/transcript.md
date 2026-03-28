# Transcript: Simplify Duplicate Printable ASCII Alphabet

## What was found

The printable ASCII range expression `Enumerable.Range(32, 95).Select(i => (char)i).ToArray()` appeared in two places:

1. `Gen.cs` — inline in `Gen.Chars()` body
2. `Generation/StringStrategy.cs` — inline in the constructor fallback `alphabet ?? Enumerable.Range(32, 95).Select(i => (char)i).ToArray()`

## What was fixed

Extracted a shared `internal static readonly char[] PrintableAscii` field in `Gen`:

```csharp
internal static readonly char[] PrintableAscii =
    Enumerable.Range(32, 95).Select(i => (char)i).ToArray();
```

- `Gen.Chars()` now uses `SampledFrom(PrintableAscii)` — no inline LINQ
- `StringStrategy` constructor now uses `alphabet ?? Gen.PrintableAscii` — no inline LINQ, dropped the `using System.Linq` import

Benefits:
- Single source of truth for the printable ASCII definition
- Array is computed once (static readonly) rather than on every `Chars()` call
- `StringStrategy` no longer needs a `using System.Linq` import

## Test results

```
dotnet test src/ --filter "FullyQualifiedName~CharStrategy|FullyQualifiedName~StringStrategy"
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4
```

All 4 tests green before and after the refactor.

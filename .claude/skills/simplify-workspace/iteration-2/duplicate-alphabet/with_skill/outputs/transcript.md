# Simplify Skill — duplicate-alphabet eval (iteration-2)

## Target file
`src/Conjecture.Core/Gen.cs`

## Before state
`Gen.Chars()` and `Gen.Strings()` each computed the printable ASCII range inline:

```csharp
// In Chars():
=> SampledFrom(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());

// In Strings():
=> new StringStrategy(
    alphabet ?? Enumerable.Range(32, 95).Select(i => (char)i).ToArray(),
    minLength, maxLength);
```

Both calls allocated a fresh `char[95]` on every invocation. The same expression appeared twice in the same file, expressing the same concept with no variation.

## Issues found

### Agent 1: Code Reuse
- `Enumerable.Range(32, 95).Select(i => (char)i).ToArray()` duplicated twice inline in `Gen.cs` — once in `Chars()` (line 52) and once in `Strings()` (line 59–61).
- No existing utility for printable-ASCII alphabet; should be extracted as a named static field.

### Agent 2: Code Quality
- Copy-paste with slight variation: identical LINQ expression in two methods of the same class. Should be unified under a single named constant that expresses intent (`PrintableAscii`).

### Agent 3: Efficiency
- Per-call allocation: both `Chars()` and `Strings()` with no alphabet argument allocated a new `char[95]` array on every call. Extracting to `static readonly` eliminates this allocation entirely.

## Fix applied

Extracted one `private static readonly char[] PrintableAscii` field at class level. Both `Chars()` and `Strings()` now reference it:

```csharp
private static readonly char[] PrintableAscii =
    Enumerable.Range(32, 95).Select(i => (char)i).ToArray();

public static Strategy<char> Chars()
    => SampledFrom(PrintableAscii);

public static Strategy<string> Strings(char[]? alphabet = null, int minLength = 0, int maxLength = 20)
    => new StringStrategy(alphabet ?? PrintableAscii, minLength, maxLength);
```

Only `src/Conjecture.Core/Gen.cs` was modified (plus `PublicAPI.Unshipped.txt` to register the new public API surface, which is required by the build enforcer — not a production logic change).

## Test results

```
dotnet test src/
Passed! — Failed: 0, Passed: 199, Skipped: 0, Total: 199, Duration: 214 ms
```

All tests pass after the change.

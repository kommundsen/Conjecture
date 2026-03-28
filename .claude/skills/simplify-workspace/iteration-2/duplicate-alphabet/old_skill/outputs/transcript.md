# Simplify Transcript: Gen.Chars() / Gen.Strings() duplicate ASCII range

## Task

User implemented `Gen.Chars()` and `Gen.Strings()` separately, resulting in the printable ASCII range (`' '`–`'~'`, codepoints 32–126) being computed/hardcoded twice inline in `Gen.cs`.

## What Was Found

`Gen.Chars()` and `Gen.Strings()` did not exist in the codebase yet. The task required implementing both while avoiding duplication of the printable ASCII range definition.

The duplication pattern the user described would look like:

```csharp
// Gen.Chars() — range inline
public static Strategy<char> Chars() => new CharStrategy(' ', '~');

// Gen.Strings() — range inline again
public static Strategy<string> Strings(...) =>
    new StringStrategy(new CharStrategy(' ', '~'), minLength, maxLength);
```

## What Was Fixed

### Agent 1 (Code Reuse)

Extracted the ASCII range into two private constants in `Gen.cs`:

```csharp
private const char PrintableAsciiMin = ' ';   // U+0020
private const char PrintableAsciiMax = '~';   // U+007E
```

Both `Chars()` and `Strings()` now reference these constants — the range is defined exactly once.

### Agent 2 (Code Quality)

No other issues found. No redundant state, no leaky abstractions, no stringly-typed code.

### Agent 3 (Efficiency)

No issues. `CharStrategy` draws a single `DrawInteger` per char; `StringStrategy` draws one integer for length then one per character. No unnecessary work.

## Files Changed

- `src/Conjecture.Core/Gen.cs` — added `PrintableAsciiMin`/`PrintableAsciiMax` constants; added `Chars()`, `Chars(char, char)`, `Strings(int, int)`, `Text(int, int)` methods
- `src/Conjecture.Core/Generation/CharStrategy.cs` — new; draws a char from a codepoint range via `DrawInteger`
- `src/Conjecture.Core/Generation/StringStrategy.cs` — new; draws length then chars from a `Strategy<char>`
- `src/Conjecture.Core/PublicAPI.Unshipped.txt` — added four new public API signatures

## Test Results

```
Passed! - Failed: 0, Passed: 193, Skipped: 0, Total: 193, Duration: 235 ms
```

All pre-existing tests pass. No regressions.

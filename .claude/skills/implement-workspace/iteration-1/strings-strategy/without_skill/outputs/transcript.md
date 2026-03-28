# StringStrategy Implementation — Without Skill Guide

## Steps taken

1. Read existing code to understand patterns:
   - `Gen.cs` — factory method style
   - `BytesStrategy.cs`, `IntegerStrategy.cs` — how strategies are structured
   - `ConjectureData.cs` — available draw methods (`DrawInteger`, `DrawBoolean`, `DrawBytes`)
   - `PublicAPI.Unshipped.txt` — format for public API declarations

2. Created test file at `src/Conjecture.Tests/Strategies/StringStrategyTests.cs` (provided verbatim).

3. Created `src/Conjecture.Core/Generation/StringStrategy.cs`:
   - `internal sealed class StringStrategy : Strategy<string>`
   - Constructor accepts `char[]? alphabet`, `int minLength`, `int maxLength`
   - Default alphabet: printable ASCII (0x20–0x7E, 95 chars)
   - `Next()`: draws length via `DrawInteger(minLength, maxLength)`, then draws each char index

4. Added `Gen.Strings()` factory method to `Gen.cs`.

5. Added public API entry to `PublicAPI.Unshipped.txt`.

6. First build failed — style violations:
   - Static field `defaultAlphabet` needed `PascalCase` (→ `DefaultAlphabet`)
   - `if` and `for` bodies needed braces (IDE0011)

7. Fixed style issues, rebuilt — clean build, 0 warnings, 0 errors.

8. Ran tests — 4/4 passed.

## Tool call counts

See metrics.json.

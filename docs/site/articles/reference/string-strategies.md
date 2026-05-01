# String strategies reference

All string generation factory methods on `Generate`.

## `Strategy.Strings`

```csharp
Strategy<string> Strategy.Strings(
    int minLength = 0,
    int maxLength = 20,
    int minCodepoint = 32,
    int maxCodepoint = 126,
    string? alphabet = null)
```

Generates strings with length in `[minLength, maxLength]`.

| Parameter | Default | Description |
|---|---|---|
| `minLength` | `0` | Minimum string length (characters). Must be ≥ 0 and ≤ `maxLength`. |
| `maxLength` | `20` | Maximum string length (characters). |
| `minCodepoint` | `32` | Minimum Unicode code point (default: space). |
| `maxCodepoint` | `126` | Maximum Unicode code point (default: `~`). Default range is printable ASCII. |
| `alphabet` | `null` | If provided, characters are drawn from this string instead of the codepoint range. |

```csharp
// Printable ASCII, 0–20 chars (default)
Strategy.Strings()

// 1–50 chars, printable ASCII
Strategy.Strings(minLength: 1, maxLength: 50)

// Letters only
Strategy.Strings(alphabet: "abcdefghijklmnopqrstuvwxyz")

// Unicode range
Strategy.Strings(minCodepoint: 0, maxCodepoint: 0x10FFFF)
```

## `Strategy.Text`

```csharp
Strategy<string> Strategy.Text(int minLength = 0, int maxLength = 20)
```

Alias for `Strategy.Strings(minLength, maxLength)` with default codepoint range.

## `Strategy.Identifiers`

```csharp
Strategy<string> Strategy.Identifiers(
    int minPrefixLength = 1,
    int maxPrefixLength = 6,
    int minDigits = 1,
    int maxDigits = 4)
```

Generates identifier-like strings of the form `[a-z]+\d+` — a lowercase alphabetic prefix followed by a numeric suffix.

| Parameter | Default | Description |
|---|---|---|
| `minPrefixLength` | `1` | Minimum length of the alphabetic prefix. Must be ≥ 1. |
| `maxPrefixLength` | `6` | Maximum length of the alphabetic prefix. |
| `minDigits` | `1` | Minimum number of digits in the numeric suffix. Must be ≥ 1. |
| `maxDigits` | `4` | Maximum number of digits in the numeric suffix. |

```csharp
Strategy.Identifiers()           // e.g. "abc123", "x9", "hello4567"
Strategy.Identifiers(1, 3, 1, 2) // e.g. "ab12", "z7"
```

The numeric suffix shrinks toward smaller numbers, and the prefix shrinks toward shorter strings — so failing identifiers reduce to minimal forms like `"a1"`.

## `Strategy.NumericStrings`

```csharp
Strategy<string> Strategy.NumericStrings(
    int minDigits = 1,
    int maxDigits = 6,
    string? prefix = null,
    string? suffix = null)
```

Generates strings consisting entirely of digits, optionally wrapped with a fixed prefix and/or suffix.

| Parameter | Default | Description |
|---|---|---|
| `minDigits` | `1` | Minimum number of digit characters. Must be ≥ 1. |
| `maxDigits` | `6` | Maximum number of digit characters. |
| `prefix` | `null` | Fixed string prepended to the digits (e.g. `"ID-"`). |
| `suffix` | `null` | Fixed string appended to the digits (e.g. `"-US"`). |

```csharp
Strategy.NumericStrings()              // e.g. "4", "10293", "7"
Strategy.NumericStrings(4, 4)          // exactly 4 digits, e.g. "0042"
Strategy.NumericStrings(prefix: "ORD-") // e.g. "ORD-123", "ORD-4"
Strategy.NumericStrings(prefix: "#", suffix: "!")  // e.g. "#42!"
```

The digit portion uses numeric-aware shrinking: it shrinks numerically (toward zero) rather than lexicographically. This means `"100"` shrinks to `"99"`, `"50"`, ..., `"1"`, `"0"` — not `"00"`, `"0"`.

## `Strategy.VersionStrings`

```csharp
Strategy<string> Strategy.VersionStrings(
    int maxMajor = 9,
    int maxMinor = 9,
    int maxPatch = 9)
```

Generates semantic version strings in `MAJOR.MINOR.PATCH` format.

| Parameter | Default | Description |
|---|---|---|
| `maxMajor` | `9` | Maximum value for the major version component. Must be ≥ 0. |
| `maxMinor` | `9` | Maximum value for the minor version component. Must be ≥ 0. |
| `maxPatch` | `9` | Maximum value for the patch version component. Must be ≥ 0. |

```csharp
Strategy.VersionStrings()            // e.g. "1.2.3", "0.0.1", "9.4.0"
Strategy.VersionStrings(99, 99, 99)  // e.g. "12.0.47"
```

Each component shrinks independently toward zero. Failing cases reduce to `"0.0.1"` or similar minimal forms.

## Choosing the right strategy

| Need | Strategy |
|---|---|
| Arbitrary text input | `Strategy.Strings()` |
| Domain-specific character set | `Strategy.Strings(alphabet: "...")` |
| Code identifiers, variable names | `Strategy.Identifiers()` |
| IDs, invoice numbers, codes | `Strategy.NumericStrings(prefix: "INV-")` |
| Version fields, semver parsing | `Strategy.VersionStrings()` |

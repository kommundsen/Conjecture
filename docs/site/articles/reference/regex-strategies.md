# Regex strategies reference

All factory methods in the `Conjecture.Regex` package.

## Package

```bash
dotnet add package Conjecture.Regex
```

Namespace: `Conjecture.Regex`

---

## `Generate.Matching`

```csharp
Strategy<string> Generate.Matching(string pattern, RegexGenOptions? options = null)
Strategy<string> Generate.Matching(Regex regex,   RegexGenOptions? options = null)
```

Returns a strategy that generates strings that match `pattern` or `regex`.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `pattern` | `string` | — | A .NET regex pattern. Compiled and cached internally. |
| `regex` | `Regex` | — | A pre-compiled `Regex` instance. `RegexOptions` are read from the instance. |
| `options` | `RegexGenOptions?` | `null` | Generation knobs; `null` uses `new RegexGenOptions()` defaults. |

The string patterns are parsed by Conjecture's own recursive-descent parser into a stable internal
AST. Generation is driven from that AST through ordinary choice-sequence IR primitives, so quantifier
counts and character indices shrink automatically alongside the generated value.

---

## `Generate.NotMatching`

```csharp
Strategy<string> Generate.NotMatching(string pattern, RegexGenOptions? options = null)
Strategy<string> Generate.NotMatching(Regex regex,   RegexGenOptions? options = null)
```

Returns a strategy that generates strings that do **not** match `pattern` or `regex`.

The strategy mutates the parsed AST to negate character classes and quantifier boundaries, then
applies a filter pass using the compiled `Regex` to discard any strings that slip through.

---

## Known-pattern shortcuts

Convenience factories for common formats. Each has a `Not*` variant.

| Method | Pattern description | Counterpart |
|---|---|---|
| `Generate.Email()` | RFC-5322 simplified address | `Generate.NotEmail()` |
| `Generate.Url()` | HTTP/HTTPS URL | `Generate.NotUrl()` |
| `Generate.Uuid()` | UUID v4 hyphenated lowercase | `Generate.NotUuid()` |
| `Generate.IsoDate()` | ISO 8601 date (`YYYY-MM-DD`) | `Generate.NotIsoDate()` |
| `Generate.CreditCard()` | 13–19 digit Luhn-valid number | `Generate.NotCreditCard()` |

The underlying patterns are exposed on `KnownRegex` as compiled `Regex` instances:

```csharp
using System.Text.RegularExpressions;
using Conjecture.Regex;

Regex emailPattern = KnownRegex.Email;
Regex urlPattern   = KnownRegex.Url;
Regex uuidPattern  = KnownRegex.Uuid;
Regex isoDatePattern = KnownRegex.IsoDate;
Regex creditCardPattern = KnownRegex.CreditCard;
```

---

## `RegexGenOptions`

```csharp
public sealed class RegexGenOptions
{
    public UnicodeCoverage UnicodeCategories { get; init; } = UnicodeCoverage.Ascii;
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `UnicodeCategories` | `UnicodeCoverage` | `Ascii` | Controls the sampling range for Unicode category escapes (`\p{L}`, `\d`, `\w`, etc.). |

### `UnicodeCoverage`

| Value | Behaviour |
|---|---|
| `Ascii` (default) | Category escapes draw from their ASCII subset. `\p{L}` → ASCII letters; `\d` → `0`–`9`. Keeps generated strings short, human-readable, and fast-shrinking. |
| `Full` | Category escapes draw from the full Unicode BMP. Use when the property genuinely depends on non-ASCII codepoints (e.g. testing a Unicode normalisation pipeline). |

```csharp
// Default: ASCII only
Strategy<string> s1 = Generate.Matching(@"\p{L}+");

// Opt in to full Unicode range
Strategy<string> s2 = Generate.Matching(
    @"\p{L}+",
    new RegexGenOptions { UnicodeCategories = UnicodeCoverage.Full });
```

---

## `RegexOptions` support

When the `Regex` overload is used, `RegexOptions` set on the instance are read intrinsically by
the parser:

| Option | Effect on generation |
|---|---|
| `IgnoreCase` | Literal characters emit both cases with equal probability. |
| `Singleline` | `.` includes `\n` in its character class. |
| `Multiline` | `^` and `$` match at line boundaries, not only string start/end. |

---

## Supported regex syntax

All standard .NET regex syntax is supported, including:

- Literal characters, character classes (`[a-z]`, `[^0-9]`)
- Shorthand escapes (`\d`, `\w`, `\s`, `\D`, `\W`, `\S`)
- Unicode categories (`\p{L}`, `\P{Lu}`)
- Quantifiers (`*`, `+`, `?`, `{m,n}`)
- Alternation (`a|b|c`)
- Groups — capturing, non-capturing, named (`(?<name>…)`)
- Lookahead / lookbehind — `(?=…)`, `(?!…)`, `(?<=…)`, `(?<!…)`
- Backreferences (`\1`, `\k<name>`)
- Conditionals (`(?(cond)yes|no)`)

Possessive quantifiers (`*+`, `++`, `?+`) are not supported by .NET's own regex engine and
are not recognised.

---

## Upcoming

**`Generate.ReDoSHunter`** — adversarial timing generation for detecting catastrophic
backtracking — is planned for v0.15.0. See [issue #366](https://github.com/kommundsen/Conjecture/issues/366).

---

## See also

- [How to test a regex validator](../how-to/test-regex-validator.md)
- [Explanation: How Conjecture.Regex works](../explanation/regex-engine.md)
- [Reference: String strategies](string-strategies.md)

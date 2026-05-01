# Regex strategies reference

All factory methods in the `Conjecture.Regex` package.

## Package

```bash
dotnet add package Conjecture.Regex
```

Namespace: `Conjecture.Regex`

---

## `Strategy.Matching`

```csharp
Strategy<string> Strategy.Matching(string pattern, RegexGenOptions? options = null)
Strategy<string> Strategy.Matching(Regex regex,   RegexGenOptions? options = null)
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

## `Strategy.NotMatching`

```csharp
Strategy<string> Strategy.NotMatching(string pattern, RegexGenOptions? options = null)
Strategy<string> Strategy.NotMatching(Regex regex,   RegexGenOptions? options = null)
```

Returns a strategy that generates strings that do **not** match `pattern` or `regex`.

The strategy mutates the parsed AST to negate character classes and quantifier boundaries, then
applies a filter pass using the compiled `Regex` to discard any strings that slip through.

---

## Known-pattern shortcuts

Convenience factories for common formats. Each has a `Not*` variant.

| Method | Pattern description | Counterpart |
|---|---|---|
| `Strategy.Email()` | RFC-5322 simplified address | `Strategy.NotEmail()` |
| `Strategy.Url()` | HTTP/HTTPS URL | `Strategy.NotUrl()` |
| `Strategy.Uuid()` | UUID v4 hyphenated lowercase | `Strategy.NotUuid()` |
| `Strategy.IsoDate()` | ISO 8601 date (`YYYY-MM-DD`) | `Strategy.NotIsoDate()` |
| `Strategy.CreditCard()` | 13–19 digit Luhn-valid number | `Strategy.NotCreditCard()` |

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
Strategy<string> s1 = Strategy.Matching(@"\p{L}+");

// Opt in to full Unicode range
Strategy<string> s2 = Strategy.Matching(
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

## `Strategy.ReDoSHunter`

```csharp
Strategy<string> Strategy.ReDoSHunter(string pattern, int maxMatchMs = 5)
Strategy<string> Strategy.ReDoSHunter(Regex regex,   int maxMatchMs = 5)
```

Returns a strategy that generates adversarial strings designed to trigger catastrophic
backtracking in `pattern` or `regex`. Use this to audit a regex for
[ReDoS vulnerabilities](../explanation/regex-catastrophic-backtracking.md) in property-based tests.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `pattern` | `string` | — | A .NET regex pattern. Compiled and cached internally. |
| `regex` | `Regex` | — | A pre-compiled `Regex` instance. `RegexOptions` are read from the instance. |
| `maxMatchMs` | `int` | `5` | Internal engine-level timeout in milliseconds. Candidates that exceed this budget on ≥ 2 of 3 trials are marked as interesting, driving Conjecture's hill-climbing loop toward slower inputs. |

### How it works

The strategy walks the `RegexNode` AST looking for nested-quantifier sub-trees (e.g. `(a+)+`,
`(a|aa)*`). When found, repetition-count draws for those nodes are biased toward the maximum to
maximise the number of partial-match backtracks. A `\0` suffix is appended to each candidate to
force trailing anchors (`$`) to fail, ensuring the engine must exhaust all backtracking paths
rather than returning on first match.

Targeting (`ctx.Target`) feeds elapsed wall-time back to Conjecture's hill-climbing loop on
every non-timeout run, steering the engine toward progressively slower inputs. A candidate is
only marked interesting after ≥ 2 out of 3 timed trials result in a `RegexMatchTimeoutException`
— this guards against spurious single-run timeouts in noisy environments.

IR-native shrinking (no custom `IShrinkPass` required) then minimises the byte buffer, converging
toward the shortest string that still triggers the timeout.

### Strategy labels

The `Label` property reflects which code path was taken:

| Label | Meaning |
|---|---|
| `"redos:hunter"` | Adversarial synthesis active — nested quantifiers found. |
| `"redos:no-nested-quantifiers"` | No nested quantifiers detected; falls back to `Strategy.Matching`. |
| `"redos:non-backtracking"` | `RegexOptions.NonBacktracking` detected; falls back to `Strategy.Matching`. The NFA engine cannot exhibit catastrophic backtracking by design. |

### `RegexOptions` handling

| Option | Behaviour |
|---|---|
| `NonBacktracking` | Falls back to `Strategy.Matching` with label `"redos:non-backtracking"`. |
| `Compiled` | No special handling — compiled regexes use the same backtracking engine. |
| All others | Passed through unchanged. |

### Example

```csharp
using System.Diagnostics;
using System.Text.RegularExpressions;
using Conjecture.Core;
using Conjecture.Regex;

[Property]
public bool UserInput_RegexIsNotVulnerableToReDoS()
{
    string input = DataGen.SampleOne(Strategy.ReDoSHunter(@"(a+)+$", maxMatchMs: 25));

    Stopwatch sw = Stopwatch.StartNew();
    Regex.IsMatch(input, @"(a+)+$");
    sw.Stop();

    return sw.ElapsedMilliseconds < 25;
}
```

---

## See also

- [How to audit a regex for catastrophic backtracking](../how-to/audit-regex-for-redos.md)
- [How to test a regex validator](../how-to/test-regex-validator.md)
- [Explanation: How Conjecture.Regex works](../explanation/regex-engine.md)
- [Explanation: Why nested quantifiers cause catastrophic backtracking](../explanation/regex-catastrophic-backtracking.md)
- [Reference: String strategies](string-strategies.md)

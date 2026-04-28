# Conjecture.Regex

Regex-based string strategies for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Generates strings that match (or deliberately don't match) a `System.Text.RegularExpressions.Regex` pattern, plus pre-canned strategies for common formats (URL, UUID, email, IP, ISO date, credit card) and a ReDoS-vulnerability hunter.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Regex
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Regex;

// Strings matching an arbitrary pattern
Strategy<string> productCodes = Generate.Matching(@"^[A-Z]{3}-\d{4}$");

// Pre-built canonical formats — shrinks toward typical short examples
Strategy<string> emails = Generate.Email();
Strategy<string> uuids = Generate.Uuid();
Strategy<string> urls = Generate.Url();
Strategy<string> isoDates = Generate.IsoDate();

// Negative space — values that don't match (useful for boundary tests)
Strategy<string> notEmail = Generate.NotEmail();

// ReDoS hunter: searches for pathological inputs that exceed maxMatchMs
Strategy<string> attacks = Generate.ReDoSHunter(@"(a+)+b", maxMatchMs: 5);
```

## API

| Method | Returns | Notes |
|---|---|---|
| `Generate.Matching(pattern)` / `Matching(regex)` | `Strategy<string>` | Generates strings the regex matches. |
| `Generate.NotMatching(pattern)` / `NotMatching(regex)` | `Strategy<string>` | Generates strings the regex does **not** match. |
| `Generate.Email()` / `Url()` / `Uuid()` / `IsoDate()` / `Date()` / `Time()` / `Ipv4()` / `Ipv6()` / `CreditCard()` | `Strategy<string>` | Canonical-format strings. |
| `Generate.NotEmail()` / `NotUrl()` / `NotUuid()` / `NotIsoDate()` / `NotCreditCard()` | `Strategy<string>` | Negative-space strings for boundary testing. |
| `Generate.ReDoSHunter(pattern, maxMatchMs)` | `Strategy<string>` | Hunts inputs that drive the regex above `maxMatchMs`. |
| `KnownRegex.Email` / `Url` / `Uuid` / etc. | `Regex` | The compiled patterns the canonical strategies use. |
| `RegexGenOptions { UnicodeCategories }` | options | Toggle `Ascii` (default) vs `Full` Unicode coverage. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)

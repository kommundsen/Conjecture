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
Strategy<string> productCodes = Strategy.Matching(@"^[A-Z]{3}-\d{4}$");

// Pre-built canonical formats — shrinks toward typical short examples
Strategy<string> emails = Strategy.Email();
Strategy<string> uuids = Strategy.Uuid();
Strategy<string> urls = Strategy.Url();
Strategy<string> isoDates = Strategy.IsoDate();

// Negative space — values that don't match (useful for boundary tests)
Strategy<string> notEmail = Strategy.NotEmail();

// ReDoS hunter: searches for pathological inputs that exceed maxMatchMs
Strategy<string> attacks = Strategy.ReDoSHunter(@"(a+)+b", maxMatchMs: 5);
```

## API

| Method | Returns | Notes |
|---|---|---|
| `Strategy.Matching(pattern)` / `Matching(regex)` | `Strategy<string>` | Generates strings the regex matches. |
| `Strategy.NotMatching(pattern)` / `NotMatching(regex)` | `Strategy<string>` | Generates strings the regex does **not** match. |
| `Strategy.Email()` / `Url()` / `Uuid()` / `IsoDate()` / `Date()` / `Time()` / `Ipv4()` / `Ipv6()` / `CreditCard()` | `Strategy<string>` | Canonical-format strings. |
| `Strategy.NotEmail()` / `NotUrl()` / `NotUuid()` / `NotIsoDate()` / `NotCreditCard()` | `Strategy<string>` | Negative-space strings for boundary testing. |
| `Strategy.ReDoSHunter(pattern, maxMatchMs)` | `Strategy<string>` | Hunts inputs that drive the regex above `maxMatchMs`. |
| `KnownRegex.Email` / `Url` / `Uuid` / etc. | `Regex` | The compiled patterns the canonical strategies use. |
| `RegexGenOptions { UnicodeCategories }` | options | Toggle `Ascii` (default) vs `Full` Unicode coverage. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
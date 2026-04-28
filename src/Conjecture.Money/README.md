# Conjecture.Money

Monetary strategies for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Provides ISO 4217 currency codes, decimal amount generation, and rounding mode strategies for deterministic financial tests.

## Install

```
dotnet add package Conjecture.Money
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Money;

// Generate valid ISO 4217 currency codes
Strategy<string> currencies = Generate.Iso4217Codes();

// Generate monetary amounts for a given currency
Strategy<decimal> amounts = Generate.Amounts(min: 0m, max: 1_000_000m, scale: 2);

// Generate rounding modes
Strategy<MidpointRounding> rounding = Generate.RoundingModes();
```

## API

| Method | Returns | Description |
|---|---|---|
| `Generate.Iso4217Codes()` | `Strategy<string>` | Active ISO 4217 alphabetic currency codes, shrinks toward `"USD"` |
| `Generate.Amounts(min, max, scale)` | `Strategy<decimal>` | Scaled decimal amounts within bounds |
| `Generate.Decimal(min, max, scale)` | `Strategy<decimal>` | General-purpose scaled decimal generation |
| `Generate.RoundingModes()` | `Strategy<MidpointRounding>` | All `MidpointRounding` enum values |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)

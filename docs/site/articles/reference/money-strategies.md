# Money strategies reference

Strategies in the `Conjecture.Money` package for generating currency codes, scaled decimal amounts, and rounding modes.

> [!NOTE]
> Requires the `Conjecture.Money` NuGet package. The `using Conjecture.Money;` import activates the extension methods on `Generate`.

## `Strategy.Iso4217Codes()`

```csharp
Strategy<string> Strategy.Iso4217Codes()
```

Samples uniformly from all 141 active ISO 4217 alphabetic currency codes (e.g. `"USD"`, `"EUR"`, `"JPY"`, `"BHD"`). Shrinks toward `"AED"` (lexicographically first active code).

Withdrawn codes (e.g. `DEM`, `FRF`, `ITL`) are excluded. The list is an embedded snapshot; it does not change at runtime.

## `Strategy.Amounts(string currencyCode, decimal min = 0m, decimal max = 10_000m)`

```csharp
Strategy<decimal> Strategy.Amounts(string currencyCode, decimal min = 0m, decimal max = 10_000m)
```

Generates decimal amounts within `[min, max]`, scaled to the minor-unit decimal places for the given ISO 4217 currency:

| Minor units | Example currencies |
|---|---|
| 0 | JPY, KRW, VND |
| 2 | USD, EUR, GBP (and most others) |
| 3 | BHD, KWD, OMR, TND |

Throws `ArgumentException` if `currencyCode` is not an active ISO 4217 code.

## `Strategy.Decimal(decimal min, decimal max, int? scale = null)`

```csharp
Strategy<decimal> Strategy.Decimal(decimal min, decimal max, int? scale = null)
```

Generates `decimal` values within `[min, max]`, optionally rounded to `scale` decimal places (0–28).

When `scale` is null the default precision of 6 decimal places is used. Shrinks toward zero.

Throws `ArgumentException` if `min > max`. Throws `ArgumentOutOfRangeException` if `scale` is outside `[0, 28]` or if the combination of range and scale exceeds `long` precision.

## `Strategy.RoundingModes()`

```csharp
Strategy<MidpointRounding> Strategy.RoundingModes()
```

Samples uniformly from all `System.MidpointRounding` enum values:
- `AwayFromZero`
- `ToEven`
- `ToNegativeInfinity`
- `ToPositiveInfinity`
- `ToZero`
- `TowardZero`

## ISO 4217 snapshot

The embedded snapshot covers 141 active codes as of 2026:
- **17 at 0 decimal places:** BIF, CLP, DJF, GNF, ISK, JPY, KMF, KRW, MGA, PYG, RWF, UGX, VND, VUV, XAF, XOF, XPF
- **117 at 2 decimal places:** AED, AFN, ALL, AMD, …, USD, EUR, GBP, …, ZWL
- **7 at 3 decimal places:** BHD, IQD, JOD, KWD, LYD, OMR, TND

## See also

- [How to test monetary and decimal arithmetic](../how-to/use-money-strategies.md)
- [Explanation: Why decimal for money testing](../explanation/money-decimal-arithmetic.md)

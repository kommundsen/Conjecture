# How to test monetary and decimal arithmetic

Generate currency codes, scaled decimal amounts, and rounding modes with `Conjecture.Money`.

## Install

```bash
dotnet add package Conjecture.Money
```

## Test rounding-mode sensitivity

Run a property across every `MidpointRounding` value to check that your calculation is consistent:

```csharp
using Conjecture.Core;
using Conjecture.Money;

[Property]
public bool TaxCalculation_SameResultRegardlessOfRoundingMode(decimal gross)
{
    // Arrange
    decimal grossAmount = DataGen.SampleOne(Strategy.Amounts("USD", min: 0m, max: 100_000m));
    MidpointRounding mode = DataGen.SampleOne(Strategy.RoundingModes());

    // Act
    decimal tax = CalculateTax(grossAmount, mode);

    // Assert — tax must always be non-negative and not exceed the gross
    return tax >= 0m && tax <= grossAmount;
}
```

`Strategy.RoundingModes()` samples all six `MidpointRounding` enum values uniformly, so a single property run exercises every rounding mode.

## Test currency-aware amount generation

Generate matched currency-code / amount pairs to verify currency-agnostic logic:

```csharp
using Conjecture.Core;
using Conjecture.Money;

[Property]
public bool Formatter_NeverThrowsForAnyActiveCurrency()
{
    string code = DataGen.SampleOne(Strategy.Iso4217Codes());
    decimal amount = DataGen.SampleOne(Strategy.Amounts(code));

    // Act — should not throw
    string formatted = MoneyFormatter.Format(amount, code);

    return formatted.Length > 0;
}
```

`Strategy.Amounts(code)` automatically uses the correct minor-unit scale for the currency (0 for JPY, 2 for USD, 3 for BHD), so generated amounts are always representable.

## Test an allocator's invariants

Use `Strategy.Decimal` with `Strategy.Integers` to verify that an allocation algorithm sums correctly:

```csharp
using Conjecture.Core;
using Conjecture.Money;

[Property]
public bool Allocator_SumsToOriginalAmount()
{
    decimal total = DataGen.SampleOne(Strategy.Amounts("USD", min: 1m, max: 10_000m));
    int parts = DataGen.SampleOne(Strategy.Integers<int>(2, 10));

    decimal[] allocated = Allocator.Split(total, parts);

    return allocated.Sum() == total && allocated.All(a => a >= 0m);
}
```

## Round-tripping currency formatting

Use `Strategy.Amounts` together with `Strategy.CulturesByCurrencyCode` to property-test that `decimal.ToString("C", culture)` round-trips through `decimal.Parse`:

```csharp
using System.Globalization;
using Conjecture.Core;
using Conjecture.Money;

[Property]
public bool CurrencyFormatting_RoundTrips()
{
    // Arrange
    decimal amount = DataGen.SampleOne(Strategy.Amounts("USD"));
    CultureInfo culture = DataGen.SampleOne(Strategy.CulturesByCurrencyCode("USD"));

    // Act
    string formatted = amount.ToString("C", culture);
    decimal parsed = decimal.Parse(formatted, NumberStyles.Currency, culture);

    // Assert — parsing the formatted string must recover the original amount
    return parsed == amount;
}
```

`Strategy.CulturesByCurrencyCode("USD")` samples from the set of `CultureInfo` instances on the current host whose currency symbol matches USD. This exercises locale-specific formatting differences — separator characters, negative-value patterns, symbol placement — that a single hard-coded culture would miss.

## See also

- [Reference: Money strategies](../reference/money-strategies.md) — full API surface
- [Explanation: Why decimal for money testing](../explanation/money-decimal-arithmetic.md) — design rationale

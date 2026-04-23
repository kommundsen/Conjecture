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
    decimal grossAmount = DataGen.SampleOne(Generate.Amounts("USD", min: 0m, max: 100_000m));
    MidpointRounding mode = DataGen.SampleOne(Generate.RoundingModes());

    // Act
    decimal tax = CalculateTax(grossAmount, mode);

    // Assert — tax must always be non-negative and not exceed the gross
    return tax >= 0m && tax <= grossAmount;
}
```

`Generate.RoundingModes()` samples all six `MidpointRounding` enum values uniformly, so a single property run exercises every rounding mode.

## Test currency-aware amount generation

Generate matched currency-code / amount pairs to verify currency-agnostic logic:

```csharp
using Conjecture.Core;
using Conjecture.Money;

[Property]
public bool Formatter_NeverThrowsForAnyActiveCurrency()
{
    string code = DataGen.SampleOne(Generate.Iso4217Codes());
    decimal amount = DataGen.SampleOne(Generate.Amounts(code));

    // Act — should not throw
    string formatted = MoneyFormatter.Format(amount, code);

    return formatted.Length > 0;
}
```

`Generate.Amounts(code)` automatically uses the correct minor-unit scale for the currency (0 for JPY, 2 for USD, 3 for BHD), so generated amounts are always representable.

## Test an allocator's invariants

Use `Generate.Decimal` with `Generate.Integers` to verify that an allocation algorithm sums correctly:

```csharp
using Conjecture.Core;
using Conjecture.Money;

[Property]
public bool Allocator_SumsToOriginalAmount()
{
    decimal total = DataGen.SampleOne(Generate.Amounts("USD", min: 1m, max: 10_000m));
    int parts = DataGen.SampleOne(Generate.Integers<int>(2, 10));

    decimal[] allocated = Allocator.Split(total, parts);

    return allocated.Sum() == total && allocated.All(a => a >= 0m);
}
```

## See also

- [Reference: Money strategies](../reference/money-strategies.md) — full API surface
- [Explanation: Why decimal for money testing](../explanation/money-decimal-arithmetic.md) — design rationale

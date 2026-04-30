// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Money;

namespace Conjecture.Money.Tests;

public class MoneyGenerateExtensionsTests
{
    [Fact]
    public void Decimal_GeneratedValuesAreWithinBounds()
    {
        decimal min = -1000m;
        decimal max = 1000m;
        Strategy<decimal> strategy = Strategy.Decimal(min, max);
        IReadOnlyList<decimal> samples = strategy.WithSeed(42UL).Sample(100);

        Assert.All(samples, v => Assert.InRange(v, min, max));
    }

    [Fact]
    public void Decimal_Scale2_AllValuesHaveAtMostTwoDecimalPlaces()
    {
        Strategy<decimal> strategy = Strategy.Decimal(0m, 100m, scale: 2);
        IReadOnlyList<decimal> samples = strategy.WithSeed(1UL).Sample(100);

        Assert.All(samples, v =>
        {
            int actualScale = (decimal.GetBits(v)[3] >> 16) & 0x1F;
            Assert.True(actualScale <= 2, $"Value {v} has scale {actualScale}, expected <= 2");
        });
    }

    [Fact]
    public void Decimal_Scale0_AllValuesAreWholeNumbers()
    {
        Strategy<decimal> strategy = Strategy.Decimal(0m, 1000m, scale: 0);
        IReadOnlyList<decimal> samples = strategy.WithSeed(2UL).Sample(100);

        Assert.All(samples, v => Assert.Equal(Math.Truncate(v), v));
    }

    [Fact]
    public void Decimal_MinEqualsMax_AlwaysReturnsThatValue()
    {
        Strategy<decimal> strategy = Strategy.Decimal(42.5m, 42.5m);
        IReadOnlyList<decimal> samples = strategy.WithSeed(3UL).Sample(20);

        Assert.All(samples, v => Assert.Equal(42.5m, v));
    }

    [Fact]
    public void Decimal_DefaultScale_ValuesSpanFullRange()
    {
        Strategy<decimal> strategy = Strategy.Decimal(-1000m, 1000m);
        IReadOnlyList<decimal> samples = strategy.WithSeed(4UL).Sample(1000);

        bool anyMoreThanTwoPlaces = samples.Any(v => ((decimal.GetBits(v)[3] >> 16) & 0x1F) > 2);
        Assert.True(anyMoreThanTwoPlaces, "Expected at least one value with more than 2 decimal places when no scale is specified");
    }

    [Fact]
    public void Decimal_AllSampledValuesAreWithinBoundsForNarrowRange()
    {
        decimal min = 10m;
        decimal max = 10.01m;
        Strategy<decimal> strategy = Strategy.Decimal(min, max);
        IReadOnlyList<decimal> samples = strategy.WithSeed(5UL).Sample(50);

        Assert.All(samples, v => Assert.InRange(v, min, max));
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Money.Internal;

namespace Conjecture.Money.Tests;

public class MoneyGenerateTests
{
    [Fact]
    public void Iso4217Codes_NeverProducesUnknownCode()
    {
        Strategy<string> strategy = Strategy.Iso4217Codes();
        IReadOnlyList<string> samples = strategy.WithSeed(10UL).Sample(500);

        Assert.All(samples, code =>
            Assert.True(Iso4217Data.DecimalPlacesByCurrency.ContainsKey(code),
                $"Code '{code}' is not in Iso4217Data"));
    }

    [Fact]
    public void Amounts_Jpy_AlwaysProducesWholeNumbers()
    {
        Strategy<decimal> strategy = Strategy.Amounts("JPY");
        IReadOnlyList<decimal> samples = strategy.WithSeed(20UL).Sample(200);

        Assert.All(samples, v =>
            Assert.True(v % 1m == 0m, $"Value {v} is not a whole number"));
    }

    [Fact]
    public void Amounts_Usd_AlwaysProducesAtMostTwoDecimalPlaces()
    {
        Strategy<decimal> strategy = Strategy.Amounts("USD");
        IReadOnlyList<decimal> samples = strategy.WithSeed(30UL).Sample(200);

        Assert.All(samples, v =>
            Assert.True(Math.Round(v, 2) == v,
                $"Value {v} has more than 2 decimal places"));
    }

    [Fact]
    public void Amounts_Bhd_AlwaysProducesAtMostThreeDecimalPlaces()
    {
        Strategy<decimal> strategy = Strategy.Amounts("BHD");
        IReadOnlyList<decimal> samples = strategy.WithSeed(40UL).Sample(200);

        Assert.All(samples, v =>
            Assert.True(Math.Round(v, 3) == v,
                $"Value {v} has more than 3 decimal places"));
    }

    [Fact]
    public void Amounts_WithMinMax_StaysWithinBounds()
    {
        decimal min = 1m;
        decimal max = 5m;
        Strategy<decimal> strategy = Strategy.Amounts("USD", min: min, max: max);
        IReadOnlyList<decimal> samples = strategy.WithSeed(50UL).Sample(200);

        Assert.All(samples, v => Assert.InRange(v, min, max));
    }

    [Fact]
    public void RoundingModes_ProducesEveryMidpointRoundingValueEventually()
    {
        Strategy<MidpointRounding> strategy = Strategy.RoundingModes();
        IReadOnlyList<MidpointRounding> samples = strategy.WithSeed(60UL).Sample(500);

        HashSet<MidpointRounding> seen = [.. samples];
        MidpointRounding[] allValues = Enum.GetValues<MidpointRounding>();

        Assert.All(allValues, mode =>
            Assert.Contains(mode, seen));
    }

    [Fact]
    public void Amounts_UnknownCurrencyCode_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Strategy.Amounts("XYZ"));
    }
}
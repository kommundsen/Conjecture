// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class IntegerStrategy_UInt128Tests
{
    [Fact]
    public void Integers_UInt128_DefaultRange_ReturnsInRange()
    {
        Strategy<UInt128> strategy = Strategy.Integers<UInt128>();
        Assert.All(
            strategy.WithSeed(42UL).Sample(100),
            v => Assert.InRange(v, UInt128.MinValue, UInt128.MaxValue));
    }

    [Fact]
    public void Integers_UInt128_DefaultRange_CoversValuesAboveULongMax()
    {
        // The old ulong-truncation bug restricts generated values to [0, ulong.MaxValue-1],
        // so values above ulong.MaxValue are never reachable. This test catches that regression.
        Strategy<UInt128> strategy = Strategy.Integers<UInt128>();
        IReadOnlyList<UInt128> samples = strategy.WithSeed(1UL).Sample(200);
        Assert.Contains(samples, v => v > (UInt128)ulong.MaxValue);
    }

    [Fact]
    public void Integers_UInt128_BoundedRange_ReturnsInRange()
    {
        UInt128 min = 0;
        UInt128 max = 999;
        Strategy<UInt128> strategy = Strategy.Integers<UInt128>(min, max);
        Assert.All(
            strategy.WithSeed(1UL).Sample(100),
            v => Assert.InRange(v, min, max));
    }

    [Fact]
    public void Integers_UInt128_RangeAboveULongMax_ReturnsInRange()
    {
        // Regression for old ulong truncation: min/max both exceed ulong.MaxValue
        UInt128 min = (UInt128)ulong.MaxValue + 1;
        UInt128 max = (UInt128)ulong.MaxValue + 1_000_000;
        Strategy<UInt128> strategy = Strategy.Integers<UInt128>(min, max);
        Assert.All(
            strategy.WithSeed(1UL).Sample(100),
            v => Assert.InRange(v, min, max));
    }

    [Fact]
    public void Integers_UInt128_MinEqualsMax_ReturnsConstant()
    {
        UInt128 value = 42;
        Strategy<UInt128> strategy = Strategy.Integers<UInt128>(value, value);
        Assert.All(strategy.WithSeed(1UL).Sample(5), v => Assert.Equal(value, v));
    }

    [Fact]
    public async Task Integers_UInt128_ShrinksTowardZero()
    {
        Strategy<UInt128> strategy = Strategy.Integers<UInt128>(UInt128.MinValue, 1_000_000);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            _ = strategy.Generate(data);
            throw new Exception("always fails");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        UInt128 shrunk = strategy.Generate(replay);
        Assert.Equal(UInt128.Zero, shrunk);
    }
}

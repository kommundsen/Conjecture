// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class IntegerStrategy_Int128Tests
{
    [Fact]
    public void Integers_Int128_DefaultRange_ReturnsInRange()
    {
        Strategy<Int128> strategy = Strategy.Integers<Int128>();
        Assert.All(
            strategy.WithSeed(42UL).Sample(100),
            v => Assert.InRange(v, Int128.MinValue, Int128.MaxValue));
    }

    [Fact]
    public void Integers_Int128_DefaultRange_CoversNegativeValues()
    {
        Strategy<Int128> strategy = Strategy.Integers<Int128>();
        IReadOnlyList<Int128> samples = strategy.WithSeed(1UL).Sample(200);
        Assert.Contains(samples, v => v < Int128.Zero);
    }

    [Fact]
    public void Integers_Int128_DefaultRange_CoversPositiveValues()
    {
        Strategy<Int128> strategy = Strategy.Integers<Int128>();
        IReadOnlyList<Int128> samples = strategy.WithSeed(1UL).Sample(200);
        Assert.Contains(samples, v => v > Int128.Zero);
    }

    [Fact]
    public void Integers_Int128_BoundedRange_ReturnsInRange()
    {
        Int128 min = new(0, 0);
        Int128 max = new(0, 999);
        Strategy<Int128> strategy = Strategy.Integers<Int128>(min, max);
        Assert.All(
            strategy.WithSeed(1UL).Sample(100),
            v => Assert.InRange(v, min, max));
    }

    [Fact]
    public void Integers_Int128_BoundedNegativeRange_ReturnsInRange()
    {
        Int128 min = -1000;
        Int128 max = -1;
        Strategy<Int128> strategy = Strategy.Integers<Int128>(min, max);
        Assert.All(
            strategy.WithSeed(1UL).Sample(100),
            v => Assert.InRange(v, min, max));
    }

    [Fact]
    public void Integers_Int128_MinEqualsMax_ReturnsConstant()
    {
        Int128 value = 12345;
        Strategy<Int128> strategy = Strategy.Integers<Int128>(value, value);
        Assert.All(strategy.WithSeed(1UL).Sample(5), v => Assert.Equal(value, v));
    }

    [Fact]
    public async Task Integers_Int128_ShrinksTowardZero_WhenZeroInRange()
    {
        Strategy<Int128> strategy = Strategy.Integers<Int128>(-1000, 1000);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Int128 v = strategy.Generate(data);
            if (v != Int128.Zero)
            {
                throw new Exception("not zero");
            }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        Int128 shrunk = strategy.Generate(replay);
        // Shrinker moves toward 0; the shrunk value should be close to 0
        Assert.True(shrunk == 1 || shrunk == -1, $"Expected shrunk near 0 but got {shrunk}");
    }

    [Fact]
    public async Task Integers_Int128_BoundedAboveZero_ShrinksTowardMin()
    {
        Int128 min = 500;
        Int128 max = 1000;
        Strategy<Int128> strategy = Strategy.Integers<Int128>(min, max);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            _ = strategy.Generate(data);
            throw new Exception("always fails");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        Int128 shrunk = strategy.Generate(replay);
        Assert.Equal(min, shrunk);
    }

    [Fact]
    public void For_Int128_ResolvesAndProducesValuesAcrossFullRange()
    {
        Strategy<Int128> strategy = Strategy.For<Int128>();
        IReadOnlyList<Int128> samples = strategy.WithSeed(7UL).Sample(200);
        Assert.All(samples, v => Assert.InRange(v, Int128.MinValue, Int128.MaxValue));
        Assert.Contains(samples, v => v < 0);
        Assert.Contains(samples, v => v > 0);
    }
}
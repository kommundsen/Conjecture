// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Numerics;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class BigIntegerStrategyTests
{
    [Fact]
    public void Integers_BigInteger_BoundedRange_ReturnsInRange()
    {
        BigInteger min = new(-100);
        BigInteger max = new(100);
        Strategy<BigInteger> strategy = Strategy.Integers(min, max);
        Assert.All(strategy.WithSeed(42UL).Sample(200), v => Assert.InRange(v, min, max));
    }

    [Fact]
    public void Integers_BigInteger_SmallRange_CoversAllValues()
    {
        BigInteger min = new(-3);
        BigInteger max = new(3);
        Strategy<BigInteger> strategy = Strategy.Integers(min, max);
        IReadOnlyList<BigInteger> samples = strategy.WithSeed(1UL).Sample(500);
        for (BigInteger expected = min; expected <= max; expected++)
        {
            Assert.Contains(samples, v => v == expected);
        }
    }

    [Fact]
    public void Integers_BigInteger_LargeRange_ProducesValuesOutsideInt128Range()
    {
        BigInteger min = BigInteger.Parse("-10000000000000000000000000000000000000000");
        BigInteger max = BigInteger.Parse("10000000000000000000000000000000000000000");
        Strategy<BigInteger> strategy = Strategy.Integers(min, max);
        IReadOnlyList<BigInteger> samples = strategy.WithSeed(1UL).Sample(1000);
        BigInteger int128Max = (BigInteger)Int128.MaxValue;
        BigInteger int128Min = (BigInteger)Int128.MinValue;
        Assert.Contains(samples, v => v > int128Max || v < int128Min);
    }

    [Fact]
    public void Integers_BigInteger_MinEqualsMax_ReturnsConstant()
    {
        BigInteger value = BigInteger.Parse("99999999999999999999");
        Strategy<BigInteger> strategy = Strategy.Integers(value, value);
        Assert.All(strategy.WithSeed(1UL).Sample(5), v => Assert.Equal(value, v));
    }

    [Fact]
    public async Task Integers_BigInteger_PositiveRange_ShrinksTowardMin()
    {
        BigInteger min = new(500);
        BigInteger max = new(1000);
        Strategy<BigInteger> strategy = Strategy.Integers(min, max);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            _ = strategy.Generate(data);
            throw new Exception("always fails");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        BigInteger shrunk = strategy.Generate(replay);
        Assert.Equal(min, shrunk);
    }

    [Fact]
    public async Task Integers_BigInteger_NegativeRange_ShrinksTowardMax()
    {
        BigInteger min = new(-1000);
        BigInteger max = new(-500);
        Strategy<BigInteger> strategy = Strategy.Integers(min, max);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            _ = strategy.Generate(data);
            throw new Exception("always fails");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        BigInteger shrunk = strategy.Generate(replay);
        Assert.Equal(max, shrunk);
    }

    [Fact]
    public async Task Integers_BigInteger_RangeContainingZero_ShrinksTowardZero()
    {
        BigInteger min = new(-1000);
        BigInteger max = new(1000);
        Strategy<BigInteger> strategy = Strategy.Integers(min, max);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL, Database = false };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            BigInteger v = strategy.Generate(data);
            if (v != BigInteger.Zero)
            {
                throw new Exception("not zero");
            }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        BigInteger shrunk = strategy.Generate(replay);
        Assert.True(shrunk == BigInteger.One || shrunk == BigInteger.MinusOne, $"Expected shrunk near 0 but got {shrunk}");
    }

    [Fact]
    public void Integers_BigInteger_MinGreaterThanMax_ThrowsArgumentOutOfRangeException()
    {
        BigInteger min = new(10);
        BigInteger max = new(5);
        Assert.Throws<ArgumentOutOfRangeException>(() => Strategy.Integers(min, max));
    }
}
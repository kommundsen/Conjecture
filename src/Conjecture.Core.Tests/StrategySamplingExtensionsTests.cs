// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Tests;

public class StrategySamplingExtensionsTests
{
    [Fact]
    public void Sample_ReturnsCorrectCount()
    {
        IReadOnlyList<int> result = Strategy.Integers<int>(0, 100).WithSeed(42UL).Sample(10);
        Assert.Equal(10, result.Count);
    }

    [Fact]
    public void Sample_SameSeed_ProducesSameOutput()
    {
        Strategy<int> strategy = Strategy.Integers<int>(0, 100);
        IReadOnlyList<int> result1 = strategy.WithSeed(42UL).Sample(10);
        IReadOnlyList<int> result2 = strategy.WithSeed(42UL).Sample(10);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Sample_DifferentSeeds_ProduceDifferentOutput()
    {
        Strategy<int> strategy = Strategy.Integers<int>(0, 100);
        IReadOnlyList<int> result1 = strategy.WithSeed(1UL).Sample(10);
        IReadOnlyList<int> result2 = strategy.WithSeed(2UL).Sample(10);
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Stream_YieldsLazily()
    {
        List<int> items = Strategy.Integers<int>(0, 100).WithSeed(42UL).Stream(100)
            .Take(5)
            .ToList();

        Assert.Equal(5, items.Count);
    }

    [Fact]
    public void SampleOne_NoArgs_ReturnsSingleValue()
    {
        int value = Strategy.Integers<int>(0, 100).WithSeed(42UL).Sample();
        Assert.InRange(value, 0, 100);
    }

    [Fact]
    public void Stream_Unbounded_OnSeededStrategy_IsLazy()
    {
        List<int> items = Strategy.Integers<int>(0, 100).WithSeed(7UL).Stream()
            .Take(3)
            .ToList();

        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void Stream_Unbounded_OnStrategy_IsLazy()
    {
        List<int> items = Strategy.Integers<int>(0, 100).Stream()
            .Take(3)
            .ToList();

        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void WithSeed_ReturnsSeededStrategy_WithSameSeed()
    {
        Strategy<int> strategy = Strategy.Integers<int>(0, 10);
        SeededStrategy<int> seeded = strategy.WithSeed(123UL);
        Assert.Equal(123UL, seeded.Seed);
        Assert.Same(strategy, seeded.Strategy);
    }
}

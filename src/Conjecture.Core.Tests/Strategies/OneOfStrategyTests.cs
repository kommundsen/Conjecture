// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class OneOfStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void OneOf_ReturnsOnlyValuesFromSuppliedStrategies()
    {
        var strategy = Generate.OneOf(Generate.Just(1), Generate.Just(2));
        var data = MakeData();

        for (var i = 0; i < 50; i++)
        {
            var value = strategy.Generate(data);
            Assert.True(value == 1 || value == 2, $"Unexpected value: {value}");
        }
    }

    [Fact]
    public void OneOf_CoversAllBranchesOverManyDraws()
    {
        var strategy = Generate.OneOf(Generate.Just(1), Generate.Just(2), Generate.Just(3));
        var data = MakeData();
        var seen = new HashSet<int>();

        for (var i = 0; i < 1000; i++)
        {
            seen.Add(strategy.Generate(data));
        }

        Assert.Contains(1, seen);
        Assert.Contains(2, seen);
        Assert.Contains(3, seen);
    }

    [Fact]
    public void OneOf_SingleStrategy_DelegatesDirectly()
    {
        var strategy = Generate.OneOf(Generate.Just(42));
        var data = MakeData();

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(42, strategy.Generate(data));
        }
    }

    [Fact]
    public void OneOf_EmptyArray_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Generate.OneOf<int>());
    }

    [Fact]
    public void OneOf_SpanOverload_ReturnsOnlyValuesFromSuppliedStrategies()
    {
        Strategy<int> strategy = Generate.OneOf(Generate.Just(1), Generate.Just(2));
        ConjectureData data = MakeData();

        for (int i = 0; i < 50; i++)
        {
            int value = strategy.Generate(data);
            Assert.True(value == 1 || value == 2, $"Unexpected value: {value}");
        }
    }

    [Fact]
    public void OneOf_SpanOverload_ThreeArgs_CoversAllBranches()
    {
        Strategy<int> strategy = Generate.OneOf(Generate.Just(1), Generate.Just(2), Generate.Just(3));
        ConjectureData data = MakeData();
        HashSet<int> seen = [];

        for (int i = 0; i < 1000; i++)
        {
            seen.Add(strategy.Generate(data));
        }

        Assert.Contains(1, seen);
        Assert.Contains(2, seen);
        Assert.Contains(3, seen);
    }

    [Fact]
    public void OneOf_SpanOverload_SixArgs_CoversAllBranches()
    {
        Strategy<int> strategy = Generate.OneOf(
            Generate.Just(1),
            Generate.Just(2),
            Generate.Just(3),
            Generate.Just(4),
            Generate.Just(5),
            Generate.Just(6));
        ConjectureData data = MakeData();
        HashSet<int> seen = [];

        for (int i = 0; i < 1000; i++)
        {
            seen.Add(strategy.Generate(data));
        }

        for (int expected = 1; expected <= 6; expected++)
        {
            Assert.Contains(expected, seen);
        }
    }

    [Fact]
    public void OneOf_SpanOverload_EmptySpan_ThrowsArgumentException()
    {
        ReadOnlySpan<Strategy<int>> span = ReadOnlySpan<Strategy<int>>.Empty;
        bool threw = false;
        try
        {
            Generate.OneOf(span);
        }
        catch (ArgumentException)
        {
            threw = true;
        }
        Assert.True(threw, "Expected ArgumentException for empty span.");
    }
}
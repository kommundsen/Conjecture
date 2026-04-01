// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

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
}
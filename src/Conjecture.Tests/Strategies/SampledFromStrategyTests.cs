// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class SampledFromStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void SampledFrom_ReturnsOnlyValuesFromSet()
    {
        var source = new[] { 1, 2, 3 };
        var strategy = Generate.SampledFrom(source);
        var data = MakeData();

        for (var i = 0; i < 100; i++)
        {
            var value = strategy.Generate(data);
            Assert.Contains(value, source);
        }
    }

    [Fact]
    public void SampledFrom_CoversAllMembersOverManyDraws()
    {
        var source = new[] { 10, 20, 30 };
        var strategy = Generate.SampledFrom(source);
        var data = MakeData();
        var seen = new HashSet<int>();

        for (var i = 0; i < 1000; i++)
        {
            seen.Add(strategy.Generate(data));
        }

        Assert.Contains(10, seen);
        Assert.Contains(20, seen);
        Assert.Contains(30, seen);
    }

    [Fact]
    public void SampledFrom_SingleElement_AlwaysReturnsThatElement()
    {
        var strategy = Generate.SampledFrom(new[] { 99 });
        var data = MakeData();

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(99, strategy.Generate(data));
        }
    }

    [Fact]
    public void SampledFrom_EmptyCollection_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Generate.SampledFrom(Array.Empty<int>()));
    }

    [Fact]
    public void SampledFrom_DeterministicWithSeed()
    {
        var source = new[] { 1, 2, 3, 4, 5 };
        var strategy = Generate.SampledFrom(source);

        var results1 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Generate(MakeData(123UL)))
            .ToList();
        var results2 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Generate(MakeData(123UL)))
            .ToList();

        Assert.Equal(results1, results2);
    }
}
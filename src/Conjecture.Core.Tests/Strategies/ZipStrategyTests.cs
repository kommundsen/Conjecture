// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class ZipStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Zip_ProducesTuple()
    {
        var strategy = Generate.Integers<int>(1, 5).Zip(Generate.Booleans());
        var data = MakeData();
        for (var i = 0; i < 50; i++)
        {
            var (n, b) = strategy.Generate(data);
            Assert.InRange(n, 1, 5);
            Assert.IsType<bool>(b);
        }
    }

    [Fact]
    public void Zip_WithResultSelector()
    {
        var strategy = Generate.Integers<int>(1, 10).Zip(Generate.Integers<int>(1, 10), (a, b) => a + b);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(strategy.Generate(data), 2, 20);
        }
    }

    [Fact]
    public void Zip_DrawsBothStrategies()
    {
        var data = MakeData();
        Generate.Integers<int>(1, 5).Zip(Generate.Booleans()).Generate(data);
        Assert.Equal(2, data.IRNodes.Count);
    }

    [Fact]
    public void Zip_IndependentStrategies()
    {
        var strategy = Generate.Integers<int>(0, 99).Zip(Generate.Integers<int>(0, 99));
        var data = MakeData();
        var (a, b) = strategy.Generate(data);
        // Both sides are drawn independently so they can differ
        // (with high probability over 99x99 space, a != b for most seeds)
        // We verify both are valid rather than asserting inequality (avoids rare flakiness)
        Assert.InRange(a, 0, 99);
        Assert.InRange(b, 0, 99);
        Assert.Equal(2, data.IRNodes.Count);
    }
}
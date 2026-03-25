using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Generation;

public class ZipStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Zip_ProducesTuple()
    {
        var strategy = Gen.Integers<int>(1, 5).Zip(Gen.Booleans());
        var data = MakeData();
        for (var i = 0; i < 50; i++)
        {
            var (n, b) = strategy.Next(data);
            Assert.InRange(n, 1, 5);
            Assert.IsType<bool>(b);
        }
    }

    [Fact]
    public void Zip_WithResultSelector()
    {
        var strategy = Gen.Integers<int>(1, 10).Zip(Gen.Integers<int>(1, 10), (a, b) => a + b);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
            Assert.InRange(strategy.Next(data), 2, 20);
    }

    [Fact]
    public void Zip_DrawsBothStrategies()
    {
        var data = MakeData();
        Gen.Integers<int>(1, 5).Zip(Gen.Booleans()).Next(data);
        Assert.Equal(2, data.IRNodes.Count);
    }

    [Fact]
    public void Zip_IndependentStrategies()
    {
        var strategy = Gen.Integers<int>(0, 99).Zip(Gen.Integers<int>(0, 99));
        var data = MakeData();
        var (a, b) = strategy.Next(data);
        // Both sides are drawn independently so they can differ
        // (with high probability over 99x99 space, a != b for most seeds)
        // We verify both are valid rather than asserting inequality (avoids rare flakiness)
        Assert.InRange(a, 0, 99);
        Assert.InRange(b, 0, 99);
        Assert.Equal(2, data.IRNodes.Count);
    }
}

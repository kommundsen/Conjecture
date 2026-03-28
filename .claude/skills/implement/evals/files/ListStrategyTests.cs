using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class ListStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void ListOf_FixedLength_ReturnsList()
    {
        var strategy = Gen.ListOf(Gen.Integers<int>(0, 100), minLength: 3, maxLength: 3);
        var result = strategy.Next(MakeData());
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ListOf_BoundedLength_ReturnsWithinBounds()
    {
        var strategy = Gen.ListOf(Gen.Booleans(), minLength: 1, maxLength: 5);
        for (var i = 0; i < 50; i++)
        {
            var list = strategy.Next(MakeData((ulong)i));
            Assert.InRange(list.Count, 1, 5);
        }
    }

    [Fact]
    public void ListOf_ElementsFromStrategy_AllInRange()
    {
        var strategy = Gen.ListOf(Gen.Integers<int>(10, 20), minLength: 5, maxLength: 10);
        var list = strategy.Next(MakeData());
        Assert.All(list, x => Assert.InRange(x, 10, 20));
    }

    [Fact]
    public void ListOf_ZeroMinLength_CanReturnEmpty()
    {
        var strategy = Gen.ListOf(Gen.Booleans(), minLength: 0, maxLength: 0);
        var result = strategy.Next(MakeData());
        Assert.Empty(result);
    }
}

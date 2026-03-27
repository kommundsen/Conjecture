using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Generation;

public class OneOfStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void OneOf_ReturnsOnlyValuesFromSuppliedStrategies()
    {
        var strategy = Gen.OneOf(Gen.Just(1), Gen.Just(2));
        var data = MakeData();

        for (var i = 0; i < 50; i++)
        {
            var value = strategy.Next(data);
            Assert.True(value == 1 || value == 2, $"Unexpected value: {value}");
        }
    }

    [Fact]
    public void OneOf_CoversAllBranchesOverManyDraws()
    {
        var strategy = Gen.OneOf(Gen.Just(1), Gen.Just(2), Gen.Just(3));
        var data = MakeData();
        var seen = new HashSet<int>();

        for (var i = 0; i < 1000; i++)
            seen.Add(strategy.Next(data));

        Assert.Contains(1, seen);
        Assert.Contains(2, seen);
        Assert.Contains(3, seen);
    }

    [Fact]
    public void OneOf_SingleStrategy_DelegatesDirectly()
    {
        var strategy = Gen.OneOf(Gen.Just(42));
        var data = MakeData();

        for (var i = 0; i < 10; i++)
            Assert.Equal(42, strategy.Next(data));
    }

    [Fact]
    public void OneOf_EmptyArray_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Gen.OneOf<int>());
    }
}

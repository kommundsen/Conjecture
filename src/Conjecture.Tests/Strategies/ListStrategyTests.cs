using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class ListStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Lists_ProducesListOfCorrectType()
    {
        var strategy = Gen.Lists(Gen.Integers<int>());
        var result = strategy.Next(MakeData());
        Assert.IsType<List<int>>(result);
    }

    [Fact]
    public void Lists_DefaultSizeVariesAcrossSeeds()
    {
        var strategy = Gen.Lists(Gen.Integers<int>());
        var sizes = new HashSet<int>();
        for (var i = 0; i < 200; i++)
        {
            sizes.Add(strategy.Next(MakeData((ulong)i)).Count);
        }
        Assert.True(sizes.Count > 5, "List sizes should vary across seeds");
    }

    [Fact]
    public void Lists_DefaultSizeWithinRange()
    {
        var strategy = Gen.Lists(Gen.Integers<int>());
        for (var i = 0; i < 200; i++)
        {
            var count = strategy.Next(MakeData((ulong)i)).Count;
            Assert.InRange(count, 0, 100);
        }
    }

    [Fact]
    public void Lists_RespectsMinSizeAndMaxSize()
    {
        var strategy = Gen.Lists(Gen.Integers<int>(), minSize: 3, maxSize: 5);
        for (var i = 0; i < 100; i++)
        {
            var count = strategy.Next(MakeData((ulong)i)).Count;
            Assert.InRange(count, 3, 5);
        }
    }

    [Fact]
    public void Lists_EmptyListPossibleWhenMinSizeIsZero()
    {
        var strategy = Gen.Lists(Gen.Integers<int>(), minSize: 0, maxSize: 10);
        var seenEmpty = false;
        for (var i = 0; i < 500; i++)
        {
            if (strategy.Next(MakeData((ulong)i)).Count == 0)
            {
                seenEmpty = true;
                break;
            }
        }
        Assert.True(seenEmpty, "Empty list should be possible when minSize=0");
    }

    [Fact]
    public void Lists_DeterministicWithSameSeed()
    {
        var strategy = Gen.Lists(Gen.Integers<int>());
        var list1 = strategy.Next(MakeData(99UL));
        var list2 = strategy.Next(MakeData(99UL));
        Assert.Equal(list1, list2);
    }

    [Fact]
    public void Lists_ElementsComefromInnerStrategy()
    {
        var strategy = Gen.Lists(Gen.Integers<int>(min: 7, max: 7), minSize: 5, maxSize: 5);
        var result = strategy.Next(MakeData());
        Assert.All(result, x => Assert.Equal(7, x));
    }
}

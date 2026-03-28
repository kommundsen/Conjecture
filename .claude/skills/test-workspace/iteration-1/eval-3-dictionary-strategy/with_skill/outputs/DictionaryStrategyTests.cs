using Conjecture.Core;
using Conjecture.Core.Internal;

#pragma warning disable CS0246 // DictionaryStrategy does not exist yet — TDD red phase

namespace Conjecture.Tests.Strategies;

public class DictionaryStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Dictionary_CountWithinBounds()
    {
        var strategy = Gen.Dictionary(Gen.Integers<int>(), Gen.Booleans(), minCount: 0, maxCount: 5);
        var data = MakeData();
        for (var i = 0; i < 200; i++)
        {
            var dict = strategy.Next(data);
            Assert.InRange(dict.Count, 0, 5);
        }
    }

    [Fact]
    public void Dictionary_AllKeysAreUnique()
    {
        var strategy = Gen.Dictionary(Gen.Integers<int>(0, 100), Gen.Integers<int>(), minCount: 1, maxCount: 5);
        var data = MakeData();
        for (var i = 0; i < 200; i++)
        {
            var dict = strategy.Next(data);
            Assert.Equal(dict.Count, dict.Keys.Distinct().Count());
        }
    }

    [Fact]
    public void Dictionary_MinCountZero_CanProduceEmptyDictionary()
    {
        var strategy = Gen.Dictionary(Gen.Integers<int>(), Gen.Booleans(), minCount: 0, maxCount: 5);
        var data = MakeData();
        var sawEmpty = false;
        for (var i = 0; i < 1000; i++)
        {
            if (strategy.Next(data).Count == 0) { sawEmpty = true; break; }
        }
        Assert.True(sawEmpty, "Expected to see at least one empty dictionary");
    }

    [Fact]
    public void Dictionary_MaxCount_NeverExceeded()
    {
        var strategy = Gen.Dictionary(Gen.Integers<int>(), Gen.Integers<int>(), minCount: 0, maxCount: 5);
        var data = MakeData();
        for (var i = 0; i < 500; i++)
        {
            Assert.True(strategy.Next(data).Count <= 5);
        }
    }
}

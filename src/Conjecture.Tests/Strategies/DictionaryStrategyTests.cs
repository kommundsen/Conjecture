using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class DictionaryStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Dictionaries_ProducesIReadOnlyDictionary()
    {
        var strategy = Gen.Dictionaries(Gen.Integers<int>(0, 100), Gen.Strings());
        var result = strategy.Next(MakeData());
        Assert.IsAssignableFrom<IReadOnlyDictionary<int, string>>(result);
    }

    [Fact]
    public void Dictionaries_KeysAreUnique()
    {
        var strategy = Gen.Dictionaries(Gen.Integers<int>(0, 100), Gen.Strings());
        for (var i = 0; i < 200; i++)
        {
            var result = strategy.Next(MakeData((ulong)i));
            Assert.Equal(result.Count, result.Keys.Distinct().Count());
        }
    }

    [Fact]
    public void Dictionaries_RespectsMinSizeAndMaxSize()
    {
        var strategy = Gen.Dictionaries(Gen.Integers<int>(0, 100), Gen.Strings(), minSize: 2, maxSize: 5);
        for (var i = 0; i < 100; i++)
        {
            var count = strategy.Next(MakeData((ulong)i)).Count;
            Assert.InRange(count, 2, 5);
        }
    }

    [Fact]
    public void Dictionaries_EmptyDictionaryPossibleWhenMinSizeIsZero()
    {
        var strategy = Gen.Dictionaries(Gen.Integers<int>(0, 100), Gen.Strings(), minSize: 0, maxSize: 10);
        var seenEmpty = false;
        for (var i = 0; i < 200; i++)
        {
            if (strategy.Next(MakeData((ulong)i)).Count == 0)
            {
                seenEmpty = true;
                break;
            }
        }
        Assert.True(seenEmpty, "Empty dictionary should be producible when minSize=0");
    }

    [Fact]
    public void Dictionaries_DeterministicWithSameSeed()
    {
        var strategy = Gen.Dictionaries(Gen.Integers<int>(0, 100), Gen.Strings());
        var dict1 = strategy.Next(MakeData(99UL));
        var dict2 = strategy.Next(MakeData(99UL));
        Assert.Equal(dict1.OrderBy(kv => kv.Key), dict2.OrderBy(kv => kv.Key));
    }
}

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

// TODO: Gen.Dictionary and DictionaryStrategy need to be implemented
public class DictionaryStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Dictionary_GeneratesWithCorrectCount()
    {
        var strategy = Gen.Dictionary(Gen.Integers<int>(), Gen.Booleans(), minCount: 0, maxCount: 5);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Next(data);
            Assert.InRange(result.Count, 0, 5);
        }
    }

    [Fact]
    public void Dictionary_HasUniqueKeys()
    {
        var strategy = Gen.Dictionary(Gen.Integers<int>(0, 1000), Gen.Integers<int>(), minCount: 1, maxCount: 5);
        var data = MakeData();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Next(data);
            var uniqueKeys = result.Keys.Distinct().Count();
            Assert.Equal(result.Count, uniqueKeys);
        }
    }
}

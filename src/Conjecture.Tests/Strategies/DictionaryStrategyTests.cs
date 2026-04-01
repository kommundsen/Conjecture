// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class DictionaryStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Dictionaries_ProducesIReadOnlyDictionary()
    {
        var strategy = Generate.Dictionaries(Generate.Integers<int>(0, 100), Generate.Strings());
        var result = strategy.Generate(MakeData());
        Assert.IsAssignableFrom<IReadOnlyDictionary<int, string>>(result);
    }

    [Fact]
    public void Dictionaries_KeysAreUnique()
    {
        var strategy = Generate.Dictionaries(Generate.Integers<int>(0, 100), Generate.Strings());
        for (var i = 0; i < 200; i++)
        {
            var result = strategy.Generate(MakeData((ulong)i));
            Assert.Equal(result.Count, result.Keys.Distinct().Count());
        }
    }

    [Fact]
    public void Dictionaries_RespectsMinSizeAndMaxSize()
    {
        var strategy = Generate.Dictionaries(Generate.Integers<int>(0, 100), Generate.Strings(), minSize: 2, maxSize: 5);
        for (var i = 0; i < 100; i++)
        {
            var count = strategy.Generate(MakeData((ulong)i)).Count;
            Assert.InRange(count, 2, 5);
        }
    }

    [Fact]
    public void Dictionaries_EmptyDictionaryPossibleWhenMinSizeIsZero()
    {
        var strategy = Generate.Dictionaries(Generate.Integers<int>(0, 100), Generate.Strings(), minSize: 0, maxSize: 10);
        var seenEmpty = false;
        for (var i = 0; i < 200; i++)
        {
            if (strategy.Generate(MakeData((ulong)i)).Count == 0)
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
        var strategy = Generate.Dictionaries(Generate.Integers<int>(0, 100), Generate.Strings());
        var dict1 = strategy.Generate(MakeData(99UL));
        var dict2 = strategy.Generate(MakeData(99UL));
        Assert.Equal(dict1.OrderBy(kv => kv.Key), dict2.OrderBy(kv => kv.Key));
    }
}
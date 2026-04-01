using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class SetStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Sets_ProducesIReadOnlySet()
    {
        var strategy = Generate.Sets(Generate.Integers<int>(0, 100));
        var result = strategy.Generate(MakeData());
        Assert.IsAssignableFrom<IReadOnlySet<int>>(result);
    }

    [Fact]
    public void Sets_AllElementsAreUnique()
    {
        var strategy = Generate.Sets(Generate.Integers<int>(0, 100));
        for (var i = 0; i < 200; i++)
        {
            var result = strategy.Generate(MakeData((ulong)i));
            Assert.Equal(result.Count, result.Distinct().Count());
        }
    }

    [Fact]
    public void Sets_DefaultSizeVariesAcrossSeeds()
    {
        var strategy = Generate.Sets(Generate.Integers<int>(0, 100));
        var sizes = new HashSet<int>();
        for (var i = 0; i < 200; i++)
        {
            sizes.Add(strategy.Generate(MakeData((ulong)i)).Count);
        }
        Assert.True(sizes.Count > 5, "Set sizes should vary across seeds");
    }

    [Fact]
    public void Sets_RespectsMinSizeAndMaxSize()
    {
        var strategy = Generate.Sets(Generate.Integers<int>(0, 100), minSize: 3, maxSize: 5);
        for (var i = 0; i < 100; i++)
        {
            var count = strategy.Generate(MakeData((ulong)i)).Count;
            Assert.InRange(count, 3, 5);
        }
    }

    [Fact]
    public void Sets_DeterministicWithSameSeed()
    {
        var strategy = Generate.Sets(Generate.Integers<int>(0, 100));
        var set1 = strategy.Generate(MakeData(99UL));
        var set2 = strategy.Generate(MakeData(99UL));
        Assert.Equal(set1.OrderBy(x => x), set2.OrderBy(x => x));
    }

    [Fact]
    public void Sets_ExhaustedUniquenessRejectionBudget_MarksInvalid()
    {
        // Inner strategy can only produce 3 distinct values (0, 1, 2); minSize=5 is impossible
        var data = MakeData();
        var strategy = Generate.Sets(Generate.Integers<int>(0, 2), minSize: 5, maxSize: 10);
        Assert.ThrowsAny<Exception>((Action)(() => strategy.Generate(data)));
        Assert.Equal(Status.Invalid, data.Status);
    }
}

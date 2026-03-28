using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Strategies;

public class SampledFromStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void SampledFrom_ReturnsOnlyValuesFromSet()
    {
        var source = new[] { 1, 2, 3 };
        var strategy = Gen.SampledFrom(source);
        var data = MakeData();

        for (var i = 0; i < 100; i++)
        {
            var value = strategy.Next(data);
            Assert.Contains(value, source);
        }
    }

    [Fact]
    public void SampledFrom_CoversAllMembersOverManyDraws()
    {
        var source = new[] { 10, 20, 30 };
        var strategy = Gen.SampledFrom(source);
        var data = MakeData();
        var seen = new HashSet<int>();

        for (var i = 0; i < 1000; i++)
        {
            seen.Add(strategy.Next(data));
        }

        Assert.Contains(10, seen);
        Assert.Contains(20, seen);
        Assert.Contains(30, seen);
    }

    [Fact]
    public void SampledFrom_SingleElement_AlwaysReturnsThatElement()
    {
        var strategy = Gen.SampledFrom(new[] { 99 });
        var data = MakeData();

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(99, strategy.Next(data));
        }
    }

    [Fact]
    public void SampledFrom_EmptyCollection_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Gen.SampledFrom(Array.Empty<int>()));
    }

    [Fact]
    public void SampledFrom_DeterministicWithSeed()
    {
        var source = new[] { 1, 2, 3, 4, 5 };
        var strategy = Gen.SampledFrom(source);

        var results1 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Next(MakeData(123UL)))
            .ToList();
        var results2 = Enumerable.Range(0, 20)
            .Select(_ => strategy.Next(MakeData(123UL)))
            .ToList();

        Assert.Equal(results1, results2);
    }
}

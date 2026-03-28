using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Strategies;

public class TupleStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Tuples_ProducesTupleWithCorrectTypes()
    {
        var strategy = Gen.Tuples(Gen.Integers<int>(), Gen.Booleans());
        var data = MakeData();
        var (n, b) = strategy.Next(data);
        Assert.IsType<int>(n);
        Assert.IsType<bool>(b);
    }

    [Fact]
    public void Tuples_BothComponentsVary()
    {
        var strategy = Gen.Tuples(Gen.Integers<int>(), Gen.Booleans());
        var ints = new HashSet<int>();
        var bools = new HashSet<bool>();
        for (var i = 0; i < 100; i++)
        {
            var data = MakeData((ulong)i);
            var (n, b) = strategy.Next(data);
            ints.Add(n);
            bools.Add(b);
        }
        Assert.True(ints.Count > 1, "Integer component should vary across seeds");
        Assert.Equal(2, bools.Count); // both true and false appear
    }

    [Fact]
    public void Tuples_DeterministicWithSameSeed()
    {
        var strategy = Gen.Tuples(Gen.Integers<int>(), Gen.Booleans());
        var (n1, b1) = strategy.Next(MakeData(99UL));
        var (n2, b2) = strategy.Next(MakeData(99UL));
        Assert.Equal(n1, n2);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void Tuples_DifferentSeedsDifferentValues()
    {
        var strategy = Gen.Tuples(Gen.Integers<int>(), Gen.Booleans());
        var results = Enumerable.Range(0, 20)
            .Select(i => strategy.Next(MakeData((ulong)i)))
            .ToList();
        Assert.True(results.Distinct().Count() > 1, "Different seeds should produce different tuples");
    }
}

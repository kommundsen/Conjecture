// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class TupleStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Tuples_ProducesTupleWithCorrectTypes()
    {
        var strategy = Generate.Tuples(Generate.Integers<int>(), Generate.Booleans());
        var data = MakeData();
        var (n, b) = strategy.Generate(data);
        Assert.IsType<int>(n);
        Assert.IsType<bool>(b);
    }

    [Fact]
    public void Tuples_BothComponentsVary()
    {
        var strategy = Generate.Tuples(Generate.Integers<int>(), Generate.Booleans());
        var ints = new HashSet<int>();
        var bools = new HashSet<bool>();
        for (var i = 0; i < 100; i++)
        {
            var data = MakeData((ulong)i);
            var (n, b) = strategy.Generate(data);
            ints.Add(n);
            bools.Add(b);
        }
        Assert.True(ints.Count > 1, "Integer component should vary across seeds");
        Assert.Equal(2, bools.Count); // both true and false appear
    }

    [Fact]
    public void Tuples_DeterministicWithSameSeed()
    {
        var strategy = Generate.Tuples(Generate.Integers<int>(), Generate.Booleans());
        var (n1, b1) = strategy.Generate(MakeData(99UL));
        var (n2, b2) = strategy.Generate(MakeData(99UL));
        Assert.Equal(n1, n2);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void Tuples_DifferentSeedsDifferentValues()
    {
        var strategy = Generate.Tuples(Generate.Integers<int>(), Generate.Booleans());
        var results = Enumerable.Range(0, 20)
            .Select(i => strategy.Generate(MakeData((ulong)i)))
            .ToList();
        Assert.True(results.Distinct().Count() > 1, "Different seeds should produce different tuples");
    }
}
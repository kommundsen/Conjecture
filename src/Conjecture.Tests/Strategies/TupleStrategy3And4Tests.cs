using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Strategies;

public class TupleStrategy3And4Tests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Tuples3_ProducesTupleWithCorrectTypes()
    {
        var strategy = Gen.Tuples(Gen.Integers<int>(), Gen.Booleans(), Gen.Integers<byte>());
        var data = MakeData();
        var (n, b, bt) = strategy.Next(data);
        Assert.IsType<int>(n);
        Assert.IsType<bool>(b);
        Assert.IsType<byte>(bt);
    }

    [Fact]
    public void Tuples3_AllComponentsVary()
    {
        var strategy = Gen.Tuples(Gen.Integers<int>(), Gen.Booleans(), Gen.Integers<short>());
        var ints = new HashSet<int>();
        var bools = new HashSet<bool>();
        var shorts = new HashSet<short>();
        for (var i = 0; i < 100; i++)
        {
            var data = MakeData((ulong)i);
            var (n, b, s) = strategy.Next(data);
            ints.Add(n);
            bools.Add(b);
            shorts.Add(s);
        }
        Assert.True(ints.Count > 1, "First component should vary across seeds");
        Assert.Equal(2, bools.Count);
        Assert.True(shorts.Count > 1, "Third component should vary across seeds");
    }

    [Fact]
    public void Tuples3_DeterministicWithSameSeed()
    {
        var strategy = Gen.Tuples(Gen.Integers<int>(), Gen.Booleans(), Gen.Integers<short>());
        var (n1, b1, s1) = strategy.Next(MakeData(99UL));
        var (n2, b2, s2) = strategy.Next(MakeData(99UL));
        Assert.Equal(n1, n2);
        Assert.Equal(b1, b2);
        Assert.Equal(s1, s2);
    }

    [Fact]
    public void Tuples4_ProducesTupleWithCorrectTypes()
    {
        var strategy = Gen.Tuples(Gen.Integers<int>(), Gen.Booleans(), Gen.Integers<byte>(), Gen.Integers<short>());
        var data = MakeData();
        var (n, b, bt, s) = strategy.Next(data);
        Assert.IsType<int>(n);
        Assert.IsType<bool>(b);
        Assert.IsType<byte>(bt);
        Assert.IsType<short>(s);
    }

    [Fact]
    public void Tuples4_AllComponentsVary()
    {
        var strategy = Gen.Tuples(Gen.Integers<int>(), Gen.Booleans(), Gen.Integers<short>(), Gen.Integers<long>());
        var ints = new HashSet<int>();
        var bools = new HashSet<bool>();
        var shorts = new HashSet<short>();
        var longs = new HashSet<long>();
        for (var i = 0; i < 100; i++)
        {
            var data = MakeData((ulong)i);
            var (n, b, s, l) = strategy.Next(data);
            ints.Add(n);
            bools.Add(b);
            shorts.Add(s);
            longs.Add(l);
        }
        Assert.True(ints.Count > 1, "First component should vary across seeds");
        Assert.Equal(2, bools.Count);
        Assert.True(shorts.Count > 1, "Third component should vary across seeds");
        Assert.True(longs.Count > 1, "Fourth component should vary across seeds");
    }

    [Fact]
    public void Tuples4_DeterministicWithSameSeed()
    {
        var strategy = Gen.Tuples(Gen.Integers<int>(), Gen.Booleans(), Gen.Integers<short>(), Gen.Integers<long>());
        var (n1, b1, s1, l1) = strategy.Next(MakeData(99UL));
        var (n2, b2, s2, l2) = strategy.Next(MakeData(99UL));
        Assert.Equal(n1, n2);
        Assert.Equal(b1, b2);
        Assert.Equal(s1, s2);
        Assert.Equal(l1, l2);
    }
}

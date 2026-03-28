using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class NullableStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void GenNullable_ProducesBothNullAndNonNull()
    {
        var strategy = Gen.Nullable(Gen.Integers<int>());
        var data = MakeData();
        var seenNull = false;
        var seenNonNull = false;

        for (var i = 0; i < 1000; i++)
        {
            var value = strategy.Next(data);
            if (value is null) { seenNull = true; }
            else { seenNonNull = true; }
            if (seenNull && seenNonNull) { break; }
        }

        Assert.True(seenNull, "Gen.Nullable never produced null");
        Assert.True(seenNonNull, "Gen.Nullable never produced non-null");
    }

    [Fact]
    public void OrNull_ProducesBothNullAndNonNull()
    {
        var strategy = Gen.Integers<int>().OrNull();
        var data = MakeData();
        var seenNull = false;
        var seenNonNull = false;

        for (var i = 0; i < 1000; i++)
        {
            var value = strategy.Next(data);
            if (value is null) { seenNull = true; }
            else { seenNonNull = true; }
            if (seenNull && seenNonNull) { break; }
        }

        Assert.True(seenNull, "OrNull() never produced null");
        Assert.True(seenNonNull, "OrNull() never produced non-null");
    }

    [Fact]
    public void GenNullable_NonNullValuesComefromInnerStrategy()
    {
        var inner = Gen.Integers<int>(min: 100, max: 100);
        var strategy = Gen.Nullable(inner);
        var data = MakeData();

        for (var i = 0; i < 200; i++)
        {
            var value = strategy.Next(data);
            if (value is not null)
            {
                Assert.Equal(100, value.Value);
            }
        }
    }

    [Fact]
    public void OrNull_NonNullValuesComefromInnerStrategy()
    {
        var strategy = Gen.Integers<int>(min: 100, max: 100).OrNull();
        var data = MakeData();

        for (var i = 0; i < 200; i++)
        {
            var value = strategy.Next(data);
            if (value is not null)
            {
                Assert.Equal(100, value.Value);
            }
        }
    }

    [Fact]
    public void GenNullable_NullProbabilityIsApproximately10Percent()
    {
        var strategy = Gen.Nullable(Gen.Integers<int>());
        var data = MakeData(seed: 12345UL);
        var nullCount = 0;
        const int trials = 1000;

        for (var i = 0; i < trials; i++)
        {
            if (strategy.Next(data) is null)
            {
                nullCount++;
            }
        }

        var nullRate = (double)nullCount / trials;
        Assert.InRange(nullRate, 0.03, 0.20);
    }
}

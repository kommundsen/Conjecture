using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Core.Generation;

namespace Conjecture.Tests.Strategies;

public class FloatingPointSpecialValuesTests
{
    private static double DrawDouble(ulong seed)
    {
        var data = ConjectureData.ForGeneration(new SplittableRandom(seed));
        return Gen.Doubles().Next(data);
    }

    private static float DrawFloat(ulong seed)
    {
        var data = ConjectureData.ForGeneration(new SplittableRandom(seed));
        return Gen.Floats().Next(data);
    }

    private static double DrawBoundedDouble(ulong seed)
    {
        var data = ConjectureData.ForGeneration(new SplittableRandom(seed));
        return Gen.Doubles(0.0, 1.0).Next(data);
    }

    [Fact]
    public void Doubles_Unbounded_CanProduceNaN()
    {
        var found = Enumerable.Range(0, 10_000).Any(s => double.IsNaN(DrawDouble((ulong)s)));
        Assert.True(found, "Expected Gen.Doubles() to produce NaN over 10,000 seeds");
    }

    [Fact]
    public void Doubles_Unbounded_CanProducePositiveInfinity()
    {
        var found = Enumerable.Range(0, 10_000).Any(s => double.IsPositiveInfinity(DrawDouble((ulong)s)));
        Assert.True(found, "Expected Gen.Doubles() to produce +Infinity over 10,000 seeds");
    }

    [Fact]
    public void Doubles_Unbounded_CanProduceNegativeInfinity()
    {
        var found = Enumerable.Range(0, 10_000).Any(s => double.IsNegativeInfinity(DrawDouble((ulong)s)));
        Assert.True(found, "Expected Gen.Doubles() to produce -Infinity over 10,000 seeds");
    }

    [Fact]
    public void Floats_Unbounded_CanProduceNaN()
    {
        var found = Enumerable.Range(0, 10_000).Any(s => float.IsNaN(DrawFloat((ulong)s)));
        Assert.True(found, "Expected Gen.Floats() to produce NaN over 10,000 seeds");
    }

    [Fact]
    public void Doubles_Bounded_NeverProducesNaN()
    {
        for (var s = 0; s < 10_000; s++)
        {
            Assert.False(double.IsNaN(DrawBoundedDouble((ulong)s)), $"Gen.Doubles(0.0, 1.0) produced NaN at seed {s}");
        }
    }

    [Fact]
    public void Doubles_Bounded_NeverProducesInfinity()
    {
        for (var s = 0; s < 10_000; s++)
        {
            Assert.False(double.IsInfinity(DrawBoundedDouble((ulong)s)), $"Gen.Doubles(0.0, 1.0) produced Infinity at seed {s}");
        }
    }

    [Fact]
    public void Doubles_Unbounded_CanProduceSubnormals()
    {
        var found = Enumerable.Range(0, 10_000).Any(s => double.IsSubnormal(DrawDouble((ulong)s)));
        Assert.True(found, "Expected Gen.Doubles() to produce subnormal values over 10,000 seeds");
    }
}

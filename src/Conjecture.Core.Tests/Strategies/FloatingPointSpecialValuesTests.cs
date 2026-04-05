// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class FloatingPointSpecialValuesTests
{
    private static double DrawDouble(ulong seed)
    {
        var data = ConjectureData.ForGeneration(new SplittableRandom(seed));
        return Generate.Doubles().Generate(data);
    }

    private static float DrawFloat(ulong seed)
    {
        var data = ConjectureData.ForGeneration(new SplittableRandom(seed));
        return Generate.Floats().Generate(data);
    }

    private static double DrawBoundedDouble(ulong seed)
    {
        var data = ConjectureData.ForGeneration(new SplittableRandom(seed));
        return Generate.Doubles(0.0, 1.0).Generate(data);
    }

    [Fact]
    public void Doubles_Unbounded_CanProduceNaN()
    {
        var found = Enumerable.Range(0, 10_000).Any(s => double.IsNaN(DrawDouble((ulong)s)));
        Assert.True(found, "Expected Generate.Doubles() to produce NaN over 10,000 seeds");
    }

    [Fact]
    public void Doubles_Unbounded_CanProducePositiveInfinity()
    {
        var found = Enumerable.Range(0, 10_000).Any(s => double.IsPositiveInfinity(DrawDouble((ulong)s)));
        Assert.True(found, "Expected Generate.Doubles() to produce +Infinity over 10,000 seeds");
    }

    [Fact]
    public void Doubles_Unbounded_CanProduceNegativeInfinity()
    {
        var found = Enumerable.Range(0, 10_000).Any(s => double.IsNegativeInfinity(DrawDouble((ulong)s)));
        Assert.True(found, "Expected Generate.Doubles() to produce -Infinity over 10,000 seeds");
    }

    [Fact]
    public void Floats_Unbounded_CanProduceNaN()
    {
        var found = Enumerable.Range(0, 10_000).Any(s => float.IsNaN(DrawFloat((ulong)s)));
        Assert.True(found, "Expected Generate.Floats() to produce NaN over 10,000 seeds");
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
        Assert.True(found, "Expected Generate.Doubles() to produce subnormal values over 10,000 seeds");
    }
}
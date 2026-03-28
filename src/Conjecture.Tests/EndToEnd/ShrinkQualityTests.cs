using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.EndToEnd;

/// <summary>
/// Smoke tests verifying the shrinker finds exact minimal counterexamples
/// for common patterns. Operates on raw IR draws to test shrinker quality
/// directly, independent of strategy logic.
/// </summary>
public class ShrinkQualityTests
{
    // --- Integer threshold ---

    [Fact]
    public void Integer_ShrinksToExactThreshold_NotThresholdPlusOne()
    {
        // Threshold = 42. Shrunk value must be exactly 42, not 43 or 0.
        var settings = new ConjectureSettings { MaxExamples = 100, Seed = 1UL };

        var result = TestRunner.Run(settings, data =>
        {
            var v = data.DrawInteger(0, 10000);
            if (v >= 42) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.Equal(42UL, result.Counterexample![0].Value);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    public void Integer_ShrinksToThreshold_AcrossSeeds(ulong seed)
    {
        var settings = new ConjectureSettings { MaxExamples = 100, Seed = seed };

        var result = TestRunner.Run(settings, data =>
        {
            var v = data.DrawInteger(0, 10000);
            if (v >= 100) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.Equal(100UL, result.Counterexample![0].Value);
    }

    // --- Two integers, minimal sum ---

    [Fact]
    public void TwoIntegers_SumExceedsThreshold_ShrinksToPreciseMinimalSum()
    {
        // a + b > 100 → minimal sum is 101.
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 3UL };

        var result = TestRunner.Run(settings, data =>
        {
            var a = data.DrawInteger(0, 200);
            var b = data.DrawInteger(0, 200);
            if (a + b > 100) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        var a = result.Counterexample![0].Value;
        var b = result.Counterexample![1].Value;
        Assert.Equal(101UL, a + b);
    }

    // --- Boolean shrinks to the failing branch ---

    [Fact]
    public void Boolean_PropertyFailsOnTrue_ShrunkValueIsTrue()
    {
        // Property only fails when the boolean is true.
        // Shrinking prefers false (value=0), but 0 doesn't fail, so 1 is minimal.
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 1UL };

        var result = TestRunner.Run(settings, data =>
        {
            var v = data.DrawBoolean();
            if (v) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        // IRNode.Value == 1 means true
        Assert.Equal(1UL, result.Counterexample![0].Value);
    }

    [Fact]
    public void Boolean_PropertyFailsOnFalse_ShrunkValueIsFalse()
    {
        // Property only fails when the boolean is false (value=0).
        // Shrinker will zero the node → 0 → false → fails → keeps it.
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 2UL };

        var result = TestRunner.Run(settings, data =>
        {
            var v = data.DrawBoolean();
            if (!v) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.Equal(0UL, result.Counterexample![0].Value);
    }

    // --- Large bounded integer shrinks within bounds ---

    [Fact]
    public void BoundedInteger_ShrinksToSmallestFailingValueWithinBounds()
    {
        // Range [500, 10000], fails when v >= 1000.
        // IRNode.Min = 500; IntegerReductionPass binary-searches [500, initial].
        // Minimal v >= 1000 is exactly 1000.
        var settings = new ConjectureSettings { MaxExamples = 100, Seed = 5UL };

        var result = TestRunner.Run(settings, data =>
        {
            var v = data.DrawInteger(500, 10000);
            if (v >= 1000) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.Equal(1000UL, result.Counterexample![0].Value);
    }

    [Fact]
    public void BoundedInteger_MinValueNeverViolatesLowerBound()
    {
        // Even after shrinking, the counterexample value must be >= the lower bound.
        var settings = new ConjectureSettings { MaxExamples = 100, Seed = 1UL };

        var result = TestRunner.Run(settings, data =>
        {
            var v = data.DrawInteger(200, 5000);
            if (v > 300) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.True(result.Counterexample![0].Value >= 200,
            $"Shrunk value {result.Counterexample![0].Value} is below lower bound 200");
        Assert.Equal(301UL, result.Counterexample![0].Value);
    }
}

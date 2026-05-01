// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.EndToEnd;

/// <summary>
/// Smoke tests verifying the shrinker finds exact minimal counterexamples
/// for common patterns. Operates on raw IR draws to test shrinker quality
/// directly, independent of strategy logic.
/// </summary>
public class ShrinkQualityTests
{
    // --- Integer threshold ---

    [Fact]
    public async Task Integer_ShrinksToExactThreshold_NotThresholdPlusOne()
    {
        // Threshold = 42. Shrunk value must be exactly 42, not 43 or 0.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 10000);
            if (v >= 42) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.Equal(42UL, result.Counterexample![0].Value);
    }

    [Property]
    [Sample(1UL)]
    [Sample(7UL)]
    [Sample(42UL)]
    public async Task Integer_ShrinksToThreshold_AcrossSeeds(ulong seed)
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = seed };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 10000);
            if (v >= 100) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.Equal(100UL, result.Counterexample![0].Value);
    }

    // --- Two integers, minimal sum ---

    [Fact]
    public async Task TwoIntegers_SumExceedsThreshold_ShrinksToPreciseMinimalSum()
    {
        // a + b > 100 → minimal sum is 101.
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 3UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong a = data.NextInteger(0, 200);
            ulong b = data.NextInteger(0, 200);
            if (a + b > 100) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        ulong a = result.Counterexample![0].Value;
        ulong b = result.Counterexample![1].Value;
        Assert.Equal(101UL, a + b);
    }

    // --- Boolean shrinks to the failing branch ---

    [Fact]
    public async Task Boolean_PropertyFailsOnTrue_ShrunkValueIsTrue()
    {
        // Property only fails when the boolean is true.
        // Shrinking prefers false (value=0), but 0 doesn't fail, so 1 is minimal.
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            bool v = data.NextBoolean();
            if (v) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        // IRNode.Value == 1 means true
        Assert.Equal(1UL, result.Counterexample![0].Value);
    }

    [Fact]
    public async Task Boolean_PropertyFailsOnFalse_ShrunkValueIsFalse()
    {
        // Property only fails when the boolean is false (value=0).
        // Shrinker will zero the node → 0 → false → fails → keeps it.
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 2UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            bool v = data.NextBoolean();
            if (!v) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.Equal(0UL, result.Counterexample![0].Value);
    }

    // --- Large bounded integer shrinks within bounds ---

    [Fact]
    public async Task BoundedInteger_ShrinksToSmallestFailingValueWithinBounds()
    {
        // Range [500, 10000], fails when v >= 1000.
        // IRNode.Min = 500; IntegerReductionPass binary-searches [500, initial].
        // Minimal v >= 1000 is exactly 1000.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 5UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(500, 10000);
            if (v >= 1000) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.Equal(1000UL, result.Counterexample![0].Value);
    }

    [Fact]
    public async Task BoundedInteger_MinValueNeverViolatesLowerBound()
    {
        // Even after shrinking, the counterexample value must be >= the lower bound.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(200, 5000);
            if (v > 300) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        Assert.True(result.Counterexample![0].Value >= 200,
            $"Shrunk value {result.Counterexample![0].Value} is below lower bound 200");
        Assert.Equal(301UL, result.Counterexample![0].Value);
    }
}
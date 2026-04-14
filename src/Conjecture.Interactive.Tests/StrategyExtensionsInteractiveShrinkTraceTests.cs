// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Core;

namespace Conjecture.Interactive.Tests;

public class StrategyExtensionsInteractiveShrinkTraceTests
{
    [Fact]
    public void ShrinkTrace_PropertyPassesOnGeneratedValue_ThrowsArgumentException()
    {
        Strategy<int> strategy = Generate.Integers<int>(0, 100);
        ulong seed = FindSeedProducingValueBelow10(strategy);

        // property returns false = value is NOT a counterexample, so nothing to shrink
        Assert.Throws<ArgumentException>(
            () => strategy.ShrinkTrace(seed, static x => x >= 10));
    }

    [Fact]
    public void ShrinkTrace_NonTrivialShrink_ReturnsAtLeastOneStep()
    {
        Strategy<int> strategy = Generate.Integers<int>(0, 100);
        ulong seed = FindSeedProducingValueAtLeast10(strategy);

        ShrinkTraceResult<int> result = strategy.ShrinkTrace(seed, static x => x >= 10);

        Assert.True(result.Steps.Count >= 1, "Expected at least one shrink step.");
    }

    [Fact]
    public void ShrinkTrace_FinalStep_ValueSatisfiesFailingProperty()
    {
        Strategy<int> strategy = Generate.Integers<int>(0, 100);
        ulong seed = FindSeedProducingValueAtLeast10(strategy);

        ShrinkTraceResult<int> result = strategy.ShrinkTrace(seed, static x => x >= 10);

        int finalValue = result.Steps[result.Steps.Count - 1].Value;
        Assert.True(finalValue >= 10, $"Final shrunk value {finalValue} should satisfy the failing property (>= 10).");
    }

    [Fact]
    public void ShrinkTrace_TextOutput_ContainsTableStructure()
    {
        Strategy<int> strategy = Generate.Integers<int>(0, 100);
        ulong seed = FindSeedProducingValueAtLeast10(strategy);

        ShrinkTraceResult<int> result = strategy.ShrinkTrace(seed, static x => x >= 10);

        Assert.Contains("Step", result.Text);
        Assert.Contains("│", result.Text);
        Assert.Contains("─", result.Text);
    }

    private static ulong FindSeedProducingValueAtLeast10(Strategy<int> strategy)
    {
        for (ulong seed = 0UL; seed < 10_000UL; seed++)
        {
            int value = DataGen.SampleOne(strategy, seed);
            if (value >= 10)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("Could not find a seed that produces a value >= 10 within 10000 tries.");
    }

    private static ulong FindSeedProducingValueBelow10(Strategy<int> strategy)
    {
        for (ulong seed = 0UL; seed < 10_000UL; seed++)
        {
            int value = DataGen.SampleOne(strategy, seed);
            if (value < 10)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("Could not find a seed that produces a value < 10 within 10000 tries.");
    }
}
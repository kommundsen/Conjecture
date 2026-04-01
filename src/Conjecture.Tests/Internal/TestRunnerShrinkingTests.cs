// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class TestRunnerShrinkingTests
{
    [Fact]
    public async Task Run_FailingProperty_CounterexampleIsShrunkTowardMinimum()
    {
        // Property fails when integer > 5. After shrinking, counterexample should be 6 (smallest failing value).
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 1000);
            if (v > 5) { throw new Exception("too big"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        // Shrunk counterexample must be the minimal failing value (6), not some large arbitrary number.
        Assert.Equal(6UL, result.Counterexample![0].Value);
    }

    [Fact]
    public async Task Run_PassingProperty_CounterexampleIsNull()
    {
        // Passing run — no shrinking should occur, no counterexample stored.
        ConjectureSettings settings = new() { MaxExamples = 20, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data => data.NextInteger(0, 100));

        Assert.True(result.Passed);
        Assert.Null(result.Counterexample);
    }

    [Fact]
    public async Task Run_ShrunkCounterexample_StillCausesPropertyToFail()
    {
        // The shrunk counterexample nodes must replay and still trigger the failure.
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 42UL };
        Exception? captured = null;

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 500);
            if (v > 3) { throw new InvalidOperationException($"fail at {v}"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);

        // Replay shrunk nodes — must still throw.
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        try
        {
            ulong v = replay.NextInteger(0, 500);
            if (v > 3) { throw new InvalidOperationException($"fail at {v}"); }
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        Assert.NotNull(captured);
    }

    [Fact]
    public async Task Run_FailingPropertyWithMultipleDraws_ShrinksBothNodes()
    {
        // Property fails when a + b > 10. Minimal counterexample should have a + b == 11.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 7UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            ulong a = data.NextInteger(0, 200);
            ulong b = data.NextInteger(0, 200);
            if (a + b > 10) { throw new Exception("sum too big"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        ulong shrunkA = result.Counterexample![0].Value;
        ulong shrunkB = result.Counterexample![1].Value;
        Assert.Equal(11UL, shrunkA + shrunkB);
    }
}
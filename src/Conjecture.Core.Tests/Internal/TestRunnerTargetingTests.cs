// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class TestRunnerTargetingTests
{
    [Fact]
    public async Task TargetingPhase_ClimbsToHigherScore()
    {
        // Generation budget = 50, targeting budget = 50.
        // HillClimber can always reach 100 from any start with 50 budget.
        var settings = new ConjectureSettings
        {
            MaxExamples = 100,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        var result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize((double)data.NextInteger(0, 100));
        });

        Assert.True(result.Passed);
        Assert.NotNull(result.TargetingScores);
        Assert.Equal(100.0, result.TargetingScores["default"]);
    }

    [Fact]
    public async Task TargetingPhase_SkippedWhenNoTargetCalls()
    {
        var settings = new ConjectureSettings
        {
            MaxExamples = 20,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        var result = await TestRunner.Run(settings, _ => { });

        Assert.True(result.Passed);
        Assert.Null(result.TargetingScores);
        // No targeting happened — all MaxExamples used for generation
        Assert.Equal(20, result.ExampleCount);
    }

    [Fact]
    public async Task TargetingPhase_SkippedWhenTargetingFalse()
    {
        var settings = new ConjectureSettings
        {
            MaxExamples = 20,
            Seed = 1UL,
            Targeting = false,
            TargetingProportion = 0.5,
        };

        var result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize((double)data.NextInteger(0, 100));
        });

        Assert.True(result.Passed);
        Assert.Null(result.TargetingScores);
        Assert.Equal(20, result.ExampleCount);
    }

    [Fact]
    public async Task TestRunResult_IncludesTargetingScores_WhenTargetingRan()
    {
        var settings = new ConjectureSettings
        {
            MaxExamples = 20,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        var result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize(42.0, "myscore");
        });

        Assert.NotNull(result.TargetingScores);
        Assert.True(result.TargetingScores.ContainsKey("myscore"));
    }

    [Fact]
    public async Task BudgetSplit_GenerationRunsProportionalExamples()
    {
        // TargetingProportion = 0.5 → generation gets 50 of 100
        var settings = new ConjectureSettings
        {
            MaxExamples = 100,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        var result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize((double)data.NextInteger(0, 100));
        });

        Assert.Equal(50, result.ExampleCount);
    }

    [Fact]
    public async Task TargetingPhase_SkippedWhenGenerationFails()
    {
        var settings = new ConjectureSettings
        {
            MaxExamples = 100,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        var result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize((double)data.NextInteger(0, 100));
            throw new InvalidOperationException("always fails");
        });

        // Targeting phase is skipped when generation found a failure.
        Assert.False(result.Passed);
        Assert.Null(result.TargetingScores);
    }
}

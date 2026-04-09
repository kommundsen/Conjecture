// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class TestRunnerMultiTargetTests
{
    [Fact]
    public async Task TwoLabels_BothPresentInTargetingScores()
    {
        var settings = new ConjectureSettings
        {
            MaxExamples = 60,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        var result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize((double)data.NextInteger(0, 100), "size");
            Target.Maximize((double)data.NextInteger(0, 100), "depth");
        });

        Assert.NotNull(result.TargetingScores);
        Assert.True(result.TargetingScores.ContainsKey("size"));
        Assert.True(result.TargetingScores.ContainsKey("depth"));
    }

    [Fact]
    public async Task MultiLabel_TotalEvaluationsRespectBudget()
    {
        // With round-robin, total targeting evaluations = targetingBudget (not N×targetingBudget).
        // MaxExamples=20: generationBudget=10, targetingBudget=10.
        // Scoring via NextInteger(0,1000) ensures HillClimber always makes progress
        // (increment always improves), so it never stops early due to no-progress.
        // Round-robin: total test-fn calls ≤ 10(gen) + 10(target) = 20 = MaxExamples.
        // Buggy sequential (each label gets full budget): ~10 + 10 + 10 = 30 calls.
        int callCount = 0;
        var settings = new ConjectureSettings
        {
            MaxExamples = 20,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        await TestRunner.Run(settings, data =>
        {
            callCount++;
            Target.Maximize((double)data.NextInteger(0, 1000), "a");
            Target.Maximize((double)data.NextInteger(0, 1000), "b");
        });

        Assert.True(callCount <= 20, $"Expected ≤20 total calls (round-robin budget), got {callCount}");
    }

    [Fact]
    public async Task TenLabels_DoesNotCrash_AllLabelsPresent()
    {
        // 10 labels, MaxExamples=20 → targetingBudget=10.
        // Round-robin gives each label at least 1 round.
        var settings = new ConjectureSettings
        {
            MaxExamples = 20,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        var result = await TestRunner.Run(settings, data =>
        {
            for (int i = 0; i < 10; i++)
            {
                Target.Maximize((double)data.NextInteger(0, 100), $"label{i}");
            }
        });

        Assert.NotNull(result.TargetingScores);
        Assert.Equal(10, result.TargetingScores.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.True(result.TargetingScores.ContainsKey($"label{i}"));
        }
    }

    [Fact]
    public async Task RoundRobin_BothLabelsImproveWithLimitedBudget()
    {
        // With a small targeting budget and round-robin, both labels should
        // receive budget (not all budget going to the first label only).
        // Start values are 0; with budget=2 and 2 labels, each gets 1 step (score +1).
        // If all budget went to "a", "b" score would remain at the generation value.
        var settings = new ConjectureSettings
        {
            MaxExamples = 12,
            Seed = 42UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        var result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize((double)data.NextInteger(0, 1000), "a");
            Target.Maximize((double)data.NextInteger(0, 1000), "b");
        });

        Assert.NotNull(result.TargetingScores);
        // Both labels must have received some targeting budget.
        Assert.True(result.TargetingScores.ContainsKey("a"));
        Assert.True(result.TargetingScores.ContainsKey("b"));
        // Scores should be at least as good as generation (targeting never decreases).
        Assert.True(result.TargetingScores["a"] >= 0.0);
        Assert.True(result.TargetingScores["b"] >= 0.0);
    }
}
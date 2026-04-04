// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;
using Xunit;

namespace Conjecture.SelfTests;

public class TargetingSelfTests
{
    // Generate.Just(0) draws no IR nodes per element, so the only IR node the HillClimber can
    // mutate is the list-size node — isolating size as the sole dimension of variation.
    private static readonly Strategy<List<int>> ListStrategy =
        Generate.Lists(Generate.Just(0), 0, 100);

    [Fact]
    public async Task HillClimbing_MonotoneScoredProperty_ScoreNeverRegressesBelowGenerationBest()
    {
        const ulong seed = 99UL;
        const int maxExamples = 100;
        const double targetingProportion = 0.5;
        int generationBudget = Math.Max(1, (int)(maxExamples * (1.0 - targetingProportion)));

        // Run generation-only phase with same seed and same generation budget to capture baseline.
        double generationBest = 0;
        ConjectureSettings genSettings = new()
        {
            MaxExamples = generationBudget,
            Seed = seed,
            Targeting = false,
            UseDatabase = false,
        };
        await TestRunner.Run(genSettings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            if (xs.Count > generationBest)
            {
                generationBest = xs.Count;
            }
        });

        ConjectureSettings targetSettings = new()
        {
            MaxExamples = maxExamples,
            Seed = seed,
            Targeting = true,
            TargetingProportion = targetingProportion,
            UseDatabase = false,
        };
        TestRunResult result = await TestRunner.Run(targetSettings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            Target.Maximize(xs.Count);
        });

        Assert.True(result.Passed);
        IReadOnlyDictionary<string, double> scores = result.TargetingScores!;
        Assert.NotNull(scores);
        double targetedScore = scores[scores.Keys.Single()];
        Assert.True(targetedScore >= generationBest,
            $"Targeting score {targetedScore} regressed below generation best {generationBest}");
    }

    [Fact]
    public async Task TargetingPhase_ExampleCount_NeverExceedsBudget()
    {
        ConjectureSettings settings = new()
        {
            MaxExamples = 100,
            Seed = 42UL,
            Targeting = true,
            TargetingProportion = 0.5,
            UseDatabase = false,
        };
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            Target.Maximize(xs.Count);
        });

        Assert.True(result.Passed);
        Assert.True(result.ExampleCount <= settings.MaxExamples,
            $"ExampleCount {result.ExampleCount} exceeds MaxExamples {settings.MaxExamples}");
    }

    [Fact]
    public async Task TargetingPhase_RecordedScores_AreAllFinite()
    {
        ConjectureSettings settings = new()
        {
            MaxExamples = 100,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
            UseDatabase = false,
        };
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            Target.Maximize(xs.Count);
        });

        Assert.True(result.Passed);
        IReadOnlyDictionary<string, double> scores = result.TargetingScores!;
        Assert.NotNull(scores);
        foreach (KeyValuePair<string, double> kvp in scores)
        {
            Assert.True(double.IsFinite(kvp.Value),
                $"Score for label '{kvp.Key}' is not finite: {kvp.Value}");
        }
    }
}

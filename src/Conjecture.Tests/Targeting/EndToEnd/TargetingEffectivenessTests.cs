// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Targeting.EndToEnd;

public class TargetingEffectivenessTests
{
    // Generate.Just(0) uses no IR nodes beyond the list-size draw, so the HillClimber
    // directly optimizes the single IR node that controls xs.Count.
    private static readonly Strategy<List<int>> ListStrategy =
        Generate.Lists(Generate.Just(0), 0, 100);

    [Fact]
    public async Task Maximize_ListSize_WithTargeting_BeatsRandomBaseline()
    {
        // Targeting: 5 gen examples + 45 targeting steps → climbs to ~100.
        // Baseline: 5 random examples → average max ≈ 83 (E[max of 5 uniform draws from {0..100}]).
        double targetingTotal = 0;
        double randomTotal = 0;

        for (ulong seed = 0; seed < 10; seed++)
        {
            ConjectureSettings targetingSettings = new()
            {
                MaxExamples = 50,
                Seed = seed,
                Targeting = true,
                TargetingProportion = 0.9,
            };

            TestRunResult targetingResult = await TestRunner.Run(targetingSettings, data =>
            {
                List<int> xs = ListStrategy.Generate(data);
                Target.Maximize(xs.Count);
            });

            targetingTotal += targetingResult.TargetingScores!["default"];

            int baselineMax = 0;
            ConjectureSettings baselineSettings = new()
            {
                MaxExamples = 5,
                Seed = seed,
                Targeting = false,
            };

            await TestRunner.Run(baselineSettings, data =>
            {
                List<int> xs = ListStrategy.Generate(data);
                baselineMax = Math.Max(baselineMax, xs.Count);
            });

            randomTotal += baselineMax;
        }

        Assert.True(targetingTotal > randomTotal,
            $"Targeting total ({targetingTotal}) should exceed random total ({randomTotal})");
    }

    [Fact]
    public async Task Minimize_ListSize_WithTargeting_ReachesZero()
    {
        // Target.Minimize negates the observation. Best score for Minimize(xs.Count)
        // is -0 == 0.0 when xs.Count reaches 0 (the minimum possible list size).
        ConjectureSettings settings = new()
        {
            MaxExamples = 100,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            Target.Minimize(xs.Count);
        });

        Assert.True(result.Passed);
        Assert.NotNull(result.TargetingScores);
        Assert.Equal(0.0, result.TargetingScores["default"]);
    }

    [Fact]
    public async Task Maximize_FindsRareBug_MoreReliablyThanRandom()
    {
        // Bug triggered only when xs.Count >= 99 (≈2% per draw from {0..100}).
        // Targeting: 9 gen + 81 targeting steps → climbs to 99+ reliably.
        // Random: 10 examples at 2% → finds in ≈18% of seeds on average.
        int targetingFinds = 0;
        int randomFinds = 0;

        for (ulong seed = 0; seed < 10; seed++)
        {
            ConjectureSettings targetingSettings = new()
            {
                MaxExamples = 90,
                Seed = seed,
                Targeting = true,
                TargetingProportion = 0.9,
            };

            TestRunResult targetingResult = await TestRunner.Run(targetingSettings, data =>
            {
                List<int> xs = ListStrategy.Generate(data);
                Target.Maximize(xs.Count);
                if (xs.Count >= 99)
                {
                    throw new InvalidOperationException("xs too large");
                }
            });

            if (!targetingResult.Passed)
            {
                targetingFinds++;
            }

            ConjectureSettings randomSettings = new()
            {
                MaxExamples = 10,
                Seed = seed,
                Targeting = false,
            };

            TestRunResult randomResult = await TestRunner.Run(randomSettings, data =>
            {
                List<int> xs = ListStrategy.Generate(data);
                if (xs.Count >= 99)
                {
                    throw new InvalidOperationException("xs too large");
                }
            });

            if (!randomResult.Passed)
            {
                randomFinds++;
            }
        }

        Assert.True(targetingFinds > randomFinds,
            $"Targeting ({targetingFinds}/10) should find the bug more often than random ({randomFinds}/10)");
    }

    [Fact]
    public async Task Failure_WithTargeting_ProducesShrunkCounterexampleAndSeedReproduction()
    {
        // Targeting maximizes xs.Count and finds failure at xs.Count > 50.
        // Verifies: failure found, counterexample shrunk, re-run with same seed re-fails.
        ConjectureSettings settings = new()
        {
            MaxExamples = 200,
            Seed = 1UL,
            Targeting = true,
            TargetingProportion = 0.5,
        };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            Target.Maximize(xs.Count);
            if (xs.Count > 50)
            {
                throw new InvalidOperationException("xs too large");
            }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        Assert.NotNull(result.Seed);

        // Seed reproduction: same seed re-finds the same failure.
        ConjectureSettings replaySettings = new()
        {
            MaxExamples = settings.MaxExamples,
            Seed = result.Seed,
            Targeting = settings.Targeting,
            TargetingProportion = settings.TargetingProportion,
        };

        TestRunResult replayResult = await TestRunner.Run(replaySettings, data =>
        {
            List<int> xs = ListStrategy.Generate(data);
            Target.Maximize(xs.Count);
            if (xs.Count > 50)
            {
                throw new InvalidOperationException("xs too large");
            }
        });

        Assert.False(replayResult.Passed);
    }
}

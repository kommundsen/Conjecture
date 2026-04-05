// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class HillClimberPerturbTests
{
    private const string Label = "score";

    // Score function with a local maximum at 50 but global maximum at 90+.
    // Greedy from 50 cannot escape since neighbors (49, 51) score lower.
    private static double LocalMaxScore(IReadOnlyList<IRNode> nodes)
    {
        var value = nodes[0].Value;
        if (value <= 50) return (double)value;
        if (value >= 80) return (double)value;
        return 0.0; // valley between 50 and 80
    }

    private static Task<(Status, IReadOnlyDictionary<string, double>)> Evaluate(
        Func<IReadOnlyList<IRNode>, double> scoreFunc,
        IReadOnlyList<IRNode> nodes)
    {
        var score = scoreFunc(nodes);
        IReadOnlyDictionary<string, double> obs = new Dictionary<string, double> { [Label] = score };
        return Task.FromResult((Status.Valid, obs));
    }

    [Fact]
    public async Task Perturbation_CanEscapeLocalMaximum()
    {
        // Start at local max (50). Greedy stalls; perturbation should escape to global max region (80+).
        var nodes = new List<IRNode> { IRNode.ForInteger(50, 0, 100) };
        var rng = new SplittableRandom(42UL);

        var (_, resultScore) = await HillClimber.Climb(
            nodes, 50.0, Label,
            n => Evaluate(LocalMaxScore, n),
            budget: 200,
            rng: rng);

        Assert.True(resultScore >= 80.0, $"Expected escape to global max region (>=80), got {resultScore}");
    }

    [Fact]
    public async Task Perturbation_AfterGreedyStalls_ImprovesByRandomJump()
    {
        // Greedy from local max at 50 stalls; with perturbation the final score improves.
        var nodes = new List<IRNode> { IRNode.ForInteger(50, 0, 100) };
        var rng = new SplittableRandom(123UL);

        var (_, resultScore) = await HillClimber.Climb(
            nodes, 50.0, Label,
            n => Evaluate(LocalMaxScore, n),
            budget: 300,
            rng: rng);

        Assert.True(resultScore > 50.0, $"Expected perturbation to improve score beyond 50, got {resultScore}");
    }

    [Fact]
    public async Task Perturbation_RespectsMinMaxBounds()
    {
        var nodes = new List<IRNode> { IRNode.ForInteger(50, 30, 70) };
        var rng = new SplittableRandom(7UL);

        var (resultNodes, _) = await HillClimber.Climb(
            nodes, 0.0, Label,
            n =>
            {
                IReadOnlyDictionary<string, double> obs =
                    new Dictionary<string, double> { [Label] = (double)n[0].Value };
                return Task.FromResult((Status.Valid, obs));
            },
            budget: 100,
            rng: rng);

        Assert.InRange(resultNodes[0].Value, 30UL, 70UL);
    }

    [Fact]
    public async Task ZeroBudget_DoesNoMutations()
    {
        var nodes = new List<IRNode> { IRNode.ForInteger(40, 0, 100) };
        var rng = new SplittableRandom(1UL);

        var (resultNodes, resultScore) = await HillClimber.Climb(
            nodes, 40.0, Label,
            n =>
            {
                IReadOnlyDictionary<string, double> obs =
                    new Dictionary<string, double> { [Label] = (double)n[0].Value };
                return Task.FromResult((Status.Valid, obs));
            },
            budget: 0,
            rng: rng);

        Assert.Equal(40UL, resultNodes[0].Value);
        Assert.Equal(40.0, resultScore);
    }
}
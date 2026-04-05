// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class HillClimberTests
{
    private static Task<(Status, IReadOnlyDictionary<string, double>)> ScoreByValue(
        string label, IReadOnlyList<IRNode> nodes)
    {
        double score = nodes.Count > 0 ? (double)nodes[0].Value : 0.0;
        var dict = new Dictionary<string, double> { [label] = score };
        return Task.FromResult<(Status, IReadOnlyDictionary<string, double>)>((Status.Valid, dict));
    }

    [Fact]
    public async Task Climb_ProducesHigherScoreThanInput()
    {
        var nodes = new List<IRNode> { IRNode.ForInteger(50, 0, 100) };
        const string label = "score";

        var (resultNodes, resultScore) = await HillClimber.Climb(
            nodes, 50.0, label,
            n => ScoreByValue(label, n),
            budget: 100);

        Assert.True(resultScore > 50.0, $"Expected score > 50, got {resultScore}");
    }

    [Fact]
    public async Task Climb_AlreadyMaximal_ReturnsSameNodes()
    {
        var nodes = new List<IRNode> { IRNode.ForInteger(100, 0, 100) };
        const string label = "score";

        var (resultNodes, resultScore) = await HillClimber.Climb(
            nodes, 100.0, label,
            n => ScoreByValue(label, n),
            budget: 50);

        Assert.Equal(100.0, resultScore);
        Assert.Equal(100UL, resultNodes[0].Value);
    }

    [Fact]
    public async Task Climb_NonIntegerLikeNodes_AreNotMutated()
    {
        var nodes = new List<IRNode>
        {
            IRNode.ForBoolean(false),
            IRNode.ForBytes(4),
        };
        const string label = "score";

        var (resultNodes, _) = await HillClimber.Climb(
            nodes, 0.0, label,
            n =>
            {
                var dict = new Dictionary<string, double> { [label] = 0.0 };
                return Task.FromResult<(Status, IReadOnlyDictionary<string, double>)>((Status.Valid, dict));
            },
            budget: 20);

        Assert.Equal(nodes[0].Value, resultNodes[0].Value);
        Assert.Equal(nodes[1].Value, resultNodes[1].Value);
    }

    [Fact]
    public async Task Climb_RespectsMinMaxBounds()
    {
        var nodes = new List<IRNode> { IRNode.ForInteger(50, 20, 80) };
        const string label = "score";

        var (resultNodes, _) = await HillClimber.Climb(
            nodes, 50.0, label,
            n => ScoreByValue(label, n),
            budget: 100);

        Assert.InRange(resultNodes[0].Value, 20UL, 80UL);
    }

    [Fact]
    public async Task Climb_MultipleIntegerNodes_TriesEachIndependently()
    {
        var nodes = new List<IRNode>
        {
            IRNode.ForInteger(10, 0, 100),
            IRNode.ForInteger(20, 0, 100),
        };
        const string label = "score";

        var (resultNodes, resultScore) = await HillClimber.Climb(
            nodes, 30.0, label,
            n =>
            {
                double sum = 0;
                foreach (var node in n)
                    if (node.IsIntegerLike) sum += node.Value;
                var dict = new Dictionary<string, double> { [label] = sum };
                return Task.FromResult<(Status, IReadOnlyDictionary<string, double>)>((Status.Valid, dict));
            },
            budget: 200);

        Assert.True(resultScore > 30.0, $"Expected score > 30, got {resultScore}");
    }

    [Fact]
    public async Task Climb_ReturnsBestNodesAndScore()
    {
        var nodes = new List<IRNode> { IRNode.ForInteger(0, 0, 50) };
        const string label = "score";

        var (resultNodes, resultScore) = await HillClimber.Climb(
            nodes, 0.0, label,
            n => ScoreByValue(label, n),
            budget: 100);

        Assert.Equal((double)resultNodes[0].Value, resultScore);
    }
}
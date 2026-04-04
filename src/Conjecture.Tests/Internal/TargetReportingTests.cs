// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class TargetReportingTests
{
    private static readonly (string, object)[] Params = [("x", (object)42)];

    [Fact]
    public void Format_WithTargetingScores_IncludesTargetScoresSection()
    {
        Dictionary<string, double> scores = new() { ["count"] = 7.0 };

        string result = CounterexampleFormatter.Format(Params, seed: 0UL, exampleCount: 1, shrinkCount: 0, targetingScores: scores);

        Assert.Contains("Target scores:", result);
        Assert.Contains("count = 7.0000", result);
    }

    [Fact]
    public void Format_WithNoTargetingScores_ExcludesTargetScoresSection()
    {
        string result = CounterexampleFormatter.Format(Params, seed: 0UL, exampleCount: 1, shrinkCount: 0, targetingScores: null);

        Assert.DoesNotContain("Target scores:", result);
    }

    [Fact]
    public void Format_WithEmptyTargetingScores_ExcludesTargetScoresSection()
    {
        string result = CounterexampleFormatter.Format(Params, seed: 0UL, exampleCount: 1, shrinkCount: 0, targetingScores: new Dictionary<string, double>());

        Assert.DoesNotContain("Target scores:", result);
    }

    [Fact]
    public void Format_WithMultipleTargetingScores_ListsAlphabetically()
    {
        Dictionary<string, double> scores = new() { ["zebra"] = 1.0, ["alpha"] = 2.0, ["beta"] = 3.0 };

        string result = CounterexampleFormatter.Format(Params, seed: 0UL, exampleCount: 1, shrinkCount: 0, targetingScores: scores);

        int alphaIdx = result.IndexOf("alpha", StringComparison.Ordinal);
        int betaIdx = result.IndexOf("beta", StringComparison.Ordinal);
        int zebraIdx = result.IndexOf("zebra", StringComparison.Ordinal);
        Assert.True(alphaIdx < betaIdx && betaIdx < zebraIdx, "Labels should be listed alphabetically");
    }

    [Fact]
    public void Format_WithOriginalAndShrunk_TargetingScoresAppended()
    {
        Dictionary<string, double> scores = new() { ["size"] = 3.14159 };

        string result = CounterexampleFormatter.Format(Params, Params, seed: 0UL, exampleCount: 5, shrinkCount: 1, targetingScores: scores);

        Assert.Contains("Target scores:", result);
        Assert.Contains("size = 3.1416", result);
    }
}

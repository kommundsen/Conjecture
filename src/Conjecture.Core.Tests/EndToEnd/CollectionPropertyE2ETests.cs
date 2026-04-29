// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.EndToEnd;

/// <summary>
/// End-to-end tests verifying that List&lt;int&gt; strategy integrates with
/// the test runner, shrinker, and failure formatter pipeline.
/// </summary>
public class CollectionPropertyE2ETests
{
    // --- Running ---

    [Fact]
    public async Task ListInt_PassingProperty_RunsWithoutError()
    {
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>(0, 100));
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data => _ = strategy.Generate(data));

        Assert.True(result.Passed);
    }

    // --- Shrinking toward empty/minimal ---

    [Fact]
    public async Task ListInt_FailsOnNonEmpty_ShrinksToSingleElement()
    {
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>(0, 10));
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 2UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            List<int> list = strategy.Generate(data);
            if (list.Count > 0)
            {
                throw new Exception("non-empty");
            }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        List<int> shrunk = strategy.Generate(replay);
        Assert.Single(shrunk);
    }

    [Fact]
    public async Task ListInt_FailsWhenElementExceedsThreshold_ShrinksToOneElementAtThreshold()
    {
        // Property fails when any element > 5. Minimal shrunk: [6] (length 1, value 6).
        Strategy<List<int>> strategy = Strategy.Lists(Strategy.Integers<int>(0, 100), minSize: 1);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 3UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            List<int> list = strategy.Generate(data);
            if (list.Any(x => x > 5))
            {
                throw new Exception("element too large");
            }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        List<int> shrunk = strategy.Generate(replay);
        int single = Assert.Single(shrunk);
        Assert.Equal(6, single);
    }

    // --- Formatted output ---

    [Fact]
    public void ListInt_FailureMessage_ContainsBracketFormattedList()
    {
        List<int> list = [1, 2, 3];
        string msg = CounterexampleFormatter.Format(
            [("xs", (object)list)],
            seed: 0UL, exampleCount: 10, shrinkCount: 2);

        Assert.Contains("xs = [1, 2, 3]", msg);
    }

    [Fact]
    public void ListInt_FailureMessage_EmptyList_ShowsEmptyBrackets()
    {
        string msg = CounterexampleFormatter.Format(
            [("xs", (object)new List<int>())],
            seed: 0UL, exampleCount: 1, shrinkCount: 0);

        Assert.Contains("xs = []", msg);
    }
}
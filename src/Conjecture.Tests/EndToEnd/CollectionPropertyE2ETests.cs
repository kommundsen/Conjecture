using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.EndToEnd;

/// <summary>
/// End-to-end tests verifying that List&lt;int&gt; strategy integrates with
/// the test runner, shrinker, and failure formatter pipeline.
/// </summary>
public class CollectionPropertyE2ETests
{
    // --- Running ---

    [Fact]
    public void ListInt_PassingProperty_RunsWithoutError()
    {
        var strategy = Gen.Lists(Gen.Integers<int>(0, 100));
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 1UL };

        var result = TestRunner.Run(settings, data =>
        {
            _ = strategy.Next(data);
        });

        Assert.True(result.Passed);
    }

    // --- Shrinking toward empty/minimal ---

    [Fact]
    public void ListInt_FailsOnNonEmpty_ShrinksToSingleElement()
    {
        var strategy = Gen.Lists(Gen.Integers<int>(0, 10));
        var settings = new ConjectureSettings { MaxExamples = 100, Seed = 2UL };

        var result = TestRunner.Run(settings, data =>
        {
            var list = strategy.Next(data);
            if (list.Count > 0) throw new Exception("non-empty");
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var shrunk = strategy.Next(replay);
        Assert.Single(shrunk);
    }

    [Fact]
    public void ListInt_FailsWhenElementExceedsThreshold_ShrinksToOneElementAtThreshold()
    {
        // Property fails when any element > 5. Minimal shrunk: [6] (length 1, value 6).
        var strategy = Gen.Lists(Gen.Integers<int>(0, 100), minSize: 1);
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 3UL };

        var result = TestRunner.Run(settings, data =>
        {
            var list = strategy.Next(data);
            if (list.Any(x => x > 5)) throw new Exception("element too large");
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var shrunk = strategy.Next(replay);
        var single = Assert.Single(shrunk);
        Assert.Equal(6, single);
    }

    // --- Formatted output ---

    [Fact]
    public void ListInt_FailureMessage_ContainsBracketFormattedList()
    {
        var list = new List<int> { 1, 2, 3 };
        var msg = CounterexampleFormatter.Format(
            [("xs", (object)list)],
            seed: 0UL, exampleCount: 10, shrinkCount: 2);

        Assert.Contains("xs = [1, 2, 3]", msg);
    }

    [Fact]
    public void ListInt_FailureMessage_EmptyList_ShowsEmptyBrackets()
    {
        var msg = CounterexampleFormatter.Format(
            [("xs", (object)new List<int>())],
            seed: 0UL, exampleCount: 1, shrinkCount: 0);

        Assert.Contains("xs = []", msg);
    }
}

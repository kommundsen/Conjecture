// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;


namespace Conjecture.Core.Tests.EndToEnd;

/// <summary>
/// End-to-end tests verifying that LINQ combinator strategies (Select, Where,
/// SelectMany, Compose) integrate correctly with the test runner and shrinker.
/// </summary>
public class CombinatorE2ETests
{
    // --- Select (map) ---

    [Fact]
    public async Task Select_FailingProperty_ShrinksThroughMapping()
    {
        // Strategy maps x -> x*2. Property fails when x*2 > 10.
        // Minimal x*2 > 10 is 12 (x=6).
        Strategy<int> strategy = Generate.Integers<int>(0, 50).Select(x => x * 2);
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v > 10) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunk = strategy.Generate(replay);
        Assert.Equal(12, shrunk);
    }

    [Fact]
    public async Task Select_PassingProperty_NoCounterexample()
    {
        Strategy<int> strategy = Generate.Integers<int>(0, 5).Select(x => x * 2); // max = 10, never > 20
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v > 20) { throw new Exception("impossible"); }
        });

        Assert.True(result.Passed);
        Assert.Null(result.Counterexample);
    }

    // --- Where (filter) ---

    [Fact]
    public async Task Where_FailingProperty_CounterexampleSatisfiesFilter()
    {
        // Only values > 0 are admitted. Property fails when value > 5.
        // Minimal counterexample must be 6 (smallest positive int > 5).
        Strategy<int> strategy = Generate.Integers<int>(0, 50).Where(x => x > 0);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 2UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        int shrunk = strategy.Generate(replay);
        Assert.Equal(6, shrunk);
    }

    [Fact]
    public async Task Where_FilteredValuesNeverReachProperty()
    {
        // All values produced by the strategy satisfy x % 2 == 0.
        // If property also checks this, it always passes.
        Strategy<int> strategy = Generate.Integers<int>(0, 20).Where(x => x % 2 == 0);
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 3UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int v = strategy.Generate(data);
            if (v % 2 != 0) { throw new Exception("odd value slipped through"); }
        });

        Assert.True(result.Passed);
    }

    // --- SelectMany (bind / query syntax) ---

    [Fact]
    public async Task SelectMany_DependentStrategy_ShrinksOuterAndInner()
    {
        // Generates (x, y) where y in [0, x]. Property fails when x > 5.
        // Shrinks to x=6, y=0 (minimal outer; inner collapses to 0).
        Strategy<(int, int)> strategy =
            from x in Generate.Integers<int>(0, 20)
            from y in Generate.Integers<int>(0, x)
            select (x, y);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 5UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            (int x, int _) = strategy.Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        (int sx, int sy) = strategy.Generate(replay);
        Assert.Equal(6, sx);
        Assert.InRange(sy, 0, sx); // y is still valid relative to x
    }

    // --- Compose (imperative) ---

    [Fact]
    public async Task Compose_DependentDraws_ShrinksToBothMinimal()
    {
        // Generates (x, y) where y in [0, x]. Property fails when x + y > 10.
        // Minimal sum > 10 is 11; shrinker finds minimal (x, y) pair.
        Strategy<(int, int)> strategy = Generate.Compose(gen =>
        {
            int x = gen.Generate(Generate.Integers<int>(0, 100));
            int y = gen.Generate(Generate.Integers<int>(0, x));
            return (x, y);
        });
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 7UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            (int x, int y) = strategy.Generate(data);
            if (x + y > 10) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        (int sx, int sy) = strategy.Generate(replay);
        Assert.Equal(11, sx + sy);
    }

    [Fact]
    public async Task Compose_WithAssume_CounterexampleSatisfiesConstraint()
    {
        // Only considers (x, y) where x > y. Property fails when x > 5.
        // Minimal x > y && x > 5 is x=6, y=0.
        Strategy<(int, int)> strategy = Generate.Compose(gen =>
        {
            int x = gen.Generate(Generate.Integers<int>(0, 20));
            int y = gen.Generate(Generate.Integers<int>(0, 20));
            gen.Assume(x > y);
            return (x, y);
        });
        ConjectureSettings settings = new() { MaxExamples = 500, Seed = 9UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            (int x, int _) = strategy.Generate(data);
            if (x > 5) { throw new Exception("fail"); }
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        (int sx, int sy) = strategy.Generate(replay);
        Assert.True(sx > sy, $"Constraint x > y violated in shrunk result: x={sx}, y={sy}");
        Assert.Equal(6, sx);
    }
}
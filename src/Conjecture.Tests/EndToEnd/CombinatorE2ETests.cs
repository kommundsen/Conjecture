using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.EndToEnd;

/// <summary>
/// End-to-end tests verifying that LINQ combinator strategies (Select, Where,
/// SelectMany, Compose) integrate correctly with the test runner and shrinker.
/// </summary>
public class CombinatorE2ETests
{
    // --- Select (map) ---

    [Fact]
    public void Select_FailingProperty_ShrinksThroughMapping()
    {
        // Strategy maps x -> x*2. Property fails when x*2 > 10.
        // Minimal x*2 > 10 is 12 (x=6).
        var strategy = Gen.Integers<int>(0, 50).Select(x => x * 2);
        var settings = new ConjectureSettings { MaxExamples = 100, Seed = 1UL };

        var result = TestRunner.Run(settings, data =>
        {
            var v = strategy.Next(data);
            if (v > 10) throw new Exception("fail");
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var shrunk = strategy.Next(replay);
        Assert.Equal(12, shrunk);
    }

    [Fact]
    public void Select_PassingProperty_NoCounterexample()
    {
        var strategy = Gen.Integers<int>(0, 5).Select(x => x * 2); // max = 10, never > 20
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 1UL };

        var result = TestRunner.Run(settings, data =>
        {
            var v = strategy.Next(data);
            if (v > 20) throw new Exception("impossible");
        });

        Assert.True(result.Passed);
        Assert.Null(result.Counterexample);
    }

    // --- Where (filter) ---

    [Fact]
    public void Where_FailingProperty_CounterexampleSatisfiesFilter()
    {
        // Only values > 0 are admitted. Property fails when value > 5.
        // Minimal counterexample must be 6 (smallest positive int > 5).
        var strategy = Gen.Integers<int>(0, 50).Where(x => x > 0);
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 2UL };

        var result = TestRunner.Run(settings, data =>
        {
            var v = strategy.Next(data);
            if (v > 5) throw new Exception("fail");
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var shrunk = strategy.Next(replay);
        Assert.Equal(6, shrunk);
    }

    [Fact]
    public void Where_FilteredValuesNeverReachProperty()
    {
        // All values produced by the strategy satisfy x % 2 == 0.
        // If property also checks this, it always passes.
        var strategy = Gen.Integers<int>(0, 20).Where(x => x % 2 == 0);
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 3UL };

        var result = TestRunner.Run(settings, data =>
        {
            var v = strategy.Next(data);
            if (v % 2 != 0) throw new Exception("odd value slipped through");
        });

        Assert.True(result.Passed);
    }

    // --- SelectMany (bind / query syntax) ---

    [Fact]
    public void SelectMany_DependentStrategy_ShrinksOuterAndInner()
    {
        // Generates (x, y) where y in [0, x]. Property fails when x > 5.
        // Shrinks to x=6, y=0 (minimal outer; inner collapses to 0).
        var strategy =
            from x in Gen.Integers<int>(0, 20)
            from y in Gen.Integers<int>(0, x)
            select (x, y);
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 5UL };

        var result = TestRunner.Run(settings, data =>
        {
            var (x, _) = strategy.Next(data);
            if (x > 5) throw new Exception("fail");
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var (sx, sy) = strategy.Next(replay);
        Assert.Equal(6, sx);
        Assert.InRange(sy, 0, sx); // y is still valid relative to x
    }

    // --- Compose (imperative) ---

    [Fact]
    public void Compose_DependentDraws_ShrinksToBothMinimal()
    {
        // Generates (x, y) where y in [0, x]. Property fails when x + y > 10.
        // Minimal sum > 10 is 11; shrinker finds minimal (x, y) pair.
        var strategy = Strategies.Compose(gen =>
        {
            var x = gen.Next(Gen.Integers<int>(0, 100));
            var y = gen.Next(Gen.Integers<int>(0, x));
            return (x, y);
        });
        var settings = new ConjectureSettings { MaxExamples = 200, Seed = 7UL };

        var result = TestRunner.Run(settings, data =>
        {
            var (x, y) = strategy.Next(data);
            if (x + y > 10) throw new Exception("fail");
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var (sx, sy) = strategy.Next(replay);
        Assert.Equal(11, sx + sy);
    }

    [Fact]
    public void Compose_WithAssume_CounterexampleSatisfiesConstraint()
    {
        // Only considers (x, y) where x > y. Property fails when x > 5.
        // Minimal x > y && x > 5 is x=6, y=0.
        var strategy = Strategies.Compose(gen =>
        {
            var x = gen.Next(Gen.Integers<int>(0, 20));
            var y = gen.Next(Gen.Integers<int>(0, 20));
            gen.Assume(x > y);
            return (x, y);
        });
        var settings = new ConjectureSettings { MaxExamples = 500, Seed = 9UL };

        var result = TestRunner.Run(settings, data =>
        {
            var (x, _) = strategy.Next(data);
            if (x > 5) throw new Exception("fail");
        });

        Assert.False(result.Passed);
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        var (sx, sy) = strategy.Next(replay);
        Assert.True(sx > sy, $"Constraint x > y violated in shrunk result: x={sx}, y={sy}");
        Assert.Equal(6, sx);
    }
}

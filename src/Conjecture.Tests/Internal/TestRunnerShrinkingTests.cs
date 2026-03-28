using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class TestRunnerShrinkingTests
{
    [Fact]
    public void Run_FailingProperty_CounterexampleIsShrunkTowardMinimum()
    {
        // Property fails when integer > 5. After shrinking, counterexample should be 6 (smallest failing value).
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 1UL };

        var result = TestRunner.Run(settings, data =>
        {
            var v = data.DrawInteger(0, 1000);
            if (v > 5) { throw new Exception("too big"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        // Shrunk counterexample must be the minimal failing value (6), not some large arbitrary number.
        Assert.Equal(6UL, result.Counterexample![0].Value);
    }

    [Fact]
    public void Run_PassingProperty_CounterexampleIsNull()
    {
        // Passing run — no shrinking should occur, no counterexample stored.
        var settings = new ConjectureSettings { MaxExamples = 20, Seed = 1UL };

        var result = TestRunner.Run(settings, data => data.DrawInteger(0, 100));

        Assert.True(result.Passed);
        Assert.Null(result.Counterexample);
    }

    [Fact]
    public void Run_ShrunkCounterexample_StillCausesPropertyToFail()
    {
        // The shrunk counterexample nodes must replay and still trigger the failure.
        var settings = new ConjectureSettings { MaxExamples = 50, Seed = 42UL };
        Exception? captured = null;

        var result = TestRunner.Run(settings, data =>
        {
            var v = data.DrawInteger(0, 500);
            if (v > 3) { throw new InvalidOperationException($"fail at {v}"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);

        // Replay shrunk nodes — must still throw.
        var replay = ConjectureData.ForRecord(result.Counterexample!);
        try
        {
            var v = replay.DrawInteger(0, 500);
            if (v > 3) { throw new InvalidOperationException($"fail at {v}"); }
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        Assert.NotNull(captured);
    }

    [Fact]
    public void Run_FailingPropertyWithMultipleDraws_ShrinksBothNodes()
    {
        // Property fails when a + b > 10. Minimal counterexample should have a + b == 11.
        var settings = new ConjectureSettings { MaxExamples = 100, Seed = 7UL };

        var result = TestRunner.Run(settings, data =>
        {
            var a = data.DrawInteger(0, 200);
            var b = data.DrawInteger(0, 200);
            if (a + b > 10) { throw new Exception("sum too big"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        var shrunkA = result.Counterexample![0].Value;
        var shrunkB = result.Counterexample![1].Value;
        Assert.Equal(11UL, shrunkA + shrunkB);
    }
}

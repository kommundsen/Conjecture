using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class TestRunnerTests
{
    [Fact]
    public void Run_AllExamplesPass_RunsExactlyMaxExamplesTimes()
    {
        var count = 0;
        var settings = new ConjectureSettings { MaxExamples = 10, Seed = 1UL };

        TestRunner.Run(settings, _ => count++);

        Assert.Equal(10, count);
    }

    [Fact]
    public void Run_AllExamplesPass_ReturnsPassingResult()
    {
        var settings = new ConjectureSettings { MaxExamples = 5, Seed = 1UL };

        var result = TestRunner.Run(settings, _ => { });

        Assert.True(result.Passed);
    }

    [Fact]
    public void Run_FirstExampleFails_StopsAfterOneInvocation()
    {
        var count = 0;
        var settings = new ConjectureSettings { MaxExamples = 100, Seed = 1UL };

        TestRunner.Run(settings, _ =>
        {
            count++;
            throw new InvalidOperationException("fail");
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public void Run_PropertyFails_ReturnsFailingResult()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, Seed = 1UL };

        var result = TestRunner.Run(settings, _ =>
            throw new InvalidOperationException("fail"));

        Assert.False(result.Passed);
    }

    [Fact]
    public void Run_PropertyFails_ResultContainsCounterexampleNodes()
    {
        var settings = new ConjectureSettings { MaxExamples = 10, Seed = 1UL };

        var result = TestRunner.Run(settings, data =>
        {
            data.DrawBoolean();
            throw new InvalidOperationException("fail");
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        Assert.NotEmpty(result.Counterexample);
    }

    [Fact]
    public void Run_SameSeed_ProducesSameCounterexampleNodes()
    {
        var settings = new ConjectureSettings { MaxExamples = 20, Seed = 99UL };
        var failOn = 5;
        var callCount = 0;

        TestRunResult RunOnce() => TestRunner.Run(settings, data =>
        {
            var v = data.DrawInteger(0, 100);
            callCount++;
            if (callCount == failOn) throw new InvalidOperationException("fail");
        });

        callCount = 0;
        var first = RunOnce();
        callCount = 0;
        var second = RunOnce();

        Assert.Equal(
            first.Counterexample!.Select(n => n.Value),
            second.Counterexample!.Select(n => n.Value));
    }

    [Fact]
    public void Run_UnsatisfiedAssumptions_NotCountedTowardMaxExamples()
    {
        var validCount = 0;
        // Throw UnsatisfiedAssumptionException for first 5 calls, then pass.
        // With MaxExamples=3, we expect exactly 3 valid runs beyond the invalid ones.
        var totalCalls = 0;
        var settings = new ConjectureSettings { MaxExamples = 3, Seed = 1UL };

        var result = TestRunner.Run(settings, _ =>
        {
            totalCalls++;
            if (totalCalls <= 5)
                throw new UnsatisfiedAssumptionException();
            validCount++;
        });

        Assert.True(result.Passed);
        Assert.Equal(3, validCount);
    }

    [Fact]
    public void Run_UnsatisfiedAssumption_DoesNotProduceFailingResult()
    {
        var settings = new ConjectureSettings { MaxExamples = 5, Seed = 1UL };

        var result = TestRunner.Run(settings, _ =>
            throw new UnsatisfiedAssumptionException());

        // All examples were invalid — still not a failure
        Assert.True(result.Passed);
    }
}

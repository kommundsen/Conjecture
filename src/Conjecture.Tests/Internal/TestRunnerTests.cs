using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class TestRunnerTests
{
    [Fact]
    public async Task Run_AllExamplesPass_RunsExactlyMaxExamplesTimes()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };

        await TestRunner.Run(settings, _ => count++);

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task Run_AllExamplesPass_ReturnsPassingResult()
    {
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, _ => { });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Run_FirstExampleFails_StopsAfterOneInvocation()
    {
        int count = 0;
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };

        await TestRunner.Run(settings, _ =>
        {
            count++;
            throw new InvalidOperationException("fail");
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Run_PropertyFails_ReturnsFailingResult()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, _ =>
            throw new InvalidOperationException("fail"));

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task Run_PropertyFails_ResultContainsCounterexampleNodes()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            data.DrawBoolean();
            throw new InvalidOperationException("fail");
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        Assert.NotEmpty(result.Counterexample);
    }

    [Fact]
    public async Task Run_SameSeed_ProducesSameCounterexampleNodes()
    {
        ConjectureSettings settings = new() { MaxExamples = 20, Seed = 99UL };
        int failOn = 5;
        int callCount = 0;

        async Task<TestRunResult> RunOnce() => await TestRunner.Run(settings, data =>
        {
            ulong v = data.DrawInteger(0, 100);
            callCount++;
            if (callCount == failOn) { throw new InvalidOperationException("fail"); }
        });

        callCount = 0;
        TestRunResult first = await RunOnce();
        callCount = 0;
        TestRunResult second = await RunOnce();

        Assert.Equal(
            first.Counterexample!.Select(n => n.Value),
            second.Counterexample!.Select(n => n.Value));
    }

    [Fact]
    public async Task Run_UnsatisfiedAssumptions_NotCountedTowardMaxExamples()
    {
        int validCount = 0;
        // Throw UnsatisfiedAssumptionException for first 5 calls, then pass.
        // With MaxExamples=3, we expect exactly 3 valid runs beyond the invalid ones.
        int totalCalls = 0;
        ConjectureSettings settings = new() { MaxExamples = 3, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, _ =>
        {
            totalCalls++;
            if (totalCalls <= 5)
            {
                throw new UnsatisfiedAssumptionException();
            }
            validCount++;
        });

        Assert.True(result.Passed);
        Assert.Equal(3, validCount);
    }

    [Fact]
    public async Task Run_UnsatisfiedAssumption_DoesNotProduceFailingResult()
    {
        ConjectureSettings settings = new() { MaxExamples = 5, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, _ =>
            throw new UnsatisfiedAssumptionException());

        // All examples were invalid — still not a failure
        Assert.True(result.Passed);
    }
}

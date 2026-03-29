using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class HealthCheckTests
{
    [Fact]
    public async Task Run_TooManyUnsatisfiedAssumptions_ThrowsConjectureException()
    {
        // First call passes (valid=1), then all subsequent calls are unsatisfied.
        // With MaxUnsatisfiedRatio=1, the second unsatisfied (unsatisfied=2 > valid=1*1) throws.
        int callCount = 0;
        ConjectureSettings settings = new() { MaxExamples = 10, MaxUnsatisfiedRatio = 1, Seed = 1UL };

        await Assert.ThrowsAsync<ConjectureException>(async () =>
            await TestRunner.Run(settings, _ =>
            {
                callCount++;
                if (callCount > 1)
                {
                    throw new UnsatisfiedAssumptionException();
                }
            }));
    }

    [Fact]
    public async Task Run_UnsatisfiedRatioWithinBudget_Passes()
    {
        int callCount = 0;
        ConjectureSettings settings = new() { MaxExamples = 10, MaxUnsatisfiedRatio = 2, Seed = 1UL };

        // Every other call is unsatisfied → ratio stays at 1, which is within budget of 2
        TestRunResult result = await TestRunner.Run(settings, _ =>
        {
            callCount++;
            if (callCount % 2 == 0)
            {
                throw new UnsatisfiedAssumptionException();
            }
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Run_MaxUnsatisfiedRatioSetting_IsRespected()
    {
        // MaxUnsatisfiedRatio=0 means zero tolerance — any unsatisfied assumption should fail
        ConjectureSettings settings = new() { MaxExamples = 10, MaxUnsatisfiedRatio = 0, Seed = 1UL };
        int callCount = 0;

        await Assert.ThrowsAsync<ConjectureException>(async () =>
            await TestRunner.Run(settings, _ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new UnsatisfiedAssumptionException();
                }
            }));
    }
}

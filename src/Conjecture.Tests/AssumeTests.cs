using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests;

public class AssumeTests
{
    [Fact]
    public void That_True_DoesNotThrow()
    {
        Assume.That(true); // no exception
    }

    [Fact]
    public void That_False_ThrowsUnsatisfiedAssumptionException()
    {
        Assert.Throws<UnsatisfiedAssumptionException>(() => Assume.That(false));
    }

    [Fact]
    public async Task That_False_InsideTestRunner_SkipsExampleAndDoesNotCountTowardMaxExamples()
    {
        int validCount = 0;
        int totalCalls = 0;
        ConjectureSettings settings = new() { MaxExamples = 3, Seed = 1UL };

        await TestRunner.Run(settings, data =>
        {
            totalCalls++;
            int x = (int)data.DrawInteger(0, 10);
            Assume.That(x % 2 == 0); // skip odd values
            validCount++;
        });

        Assert.Equal(3, validCount);
        Assert.True(totalCalls > 3); // some calls were skipped
    }
}

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
    public void That_False_InsideTestRunner_SkipsExampleAndDoesNotCountTowardMaxExamples()
    {
        var validCount = 0;
        var totalCalls = 0;
        var settings = new ConjectureSettings { MaxExamples = 3, Seed = 1UL };

        TestRunner.Run(settings, data =>
        {
            totalCalls++;
            var x = (int)data.DrawInteger(0, 10);
            Assume.That(x % 2 == 0); // skip odd values
            validCount++;
        });

        Assert.Equal(3, validCount);
        Assert.True(totalCalls > 3); // some calls were skipped
    }
}

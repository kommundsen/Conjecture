using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit;

namespace Conjecture.Xunit.Tests;

public class PropertyAttributeSettingsTests
{
    [Fact]
    public void UseDatabase_DefaultsToTrue()
    {
        var attr = new PropertyAttribute();

        Assert.True(attr.UseDatabase);
    }

    [Fact]
    public void UseDatabase_CanBeSetToFalse()
    {
        var attr = new PropertyAttribute { UseDatabase = false };

        Assert.False(attr.UseDatabase);
    }

    [Fact]
    public void MaxStrategyRejections_DefaultsFive()
    {
        var attr = new PropertyAttribute();

        Assert.Equal(5, attr.MaxStrategyRejections);
    }

    [Fact]
    public void MaxStrategyRejections_CanBeSet()
    {
        var attr = new PropertyAttribute { MaxStrategyRejections = 20 };

        Assert.Equal(20, attr.MaxStrategyRejections);
    }

    [Fact]
    public void DeadlineMs_DefaultsZero()
    {
        var attr = new PropertyAttribute();

        Assert.Equal(0, attr.DeadlineMs);
    }

    [Fact]
    public void DeadlineMs_CanBeSet()
    {
        var attr = new PropertyAttribute { DeadlineMs = 500 };

        Assert.Equal(500, attr.DeadlineMs);
    }

    [Fact]
    public void TestRunner_Deadline_TerminatesLongRunningTest()
    {
        var settings = new ConjectureSettings
        {
            MaxExamples = 10_000,
            Deadline = TimeSpan.FromMilliseconds(50),
        };

        Assert.Throws<ConjectureException>(() =>
            TestRunner.Run(settings, data =>
            {
                _ = data.DrawInteger(0, 100);
                Thread.Sleep(10);
            }));
    }
}

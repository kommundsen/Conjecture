using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Xunit;

namespace Conjecture.Xunit.Tests;

public class PropertyAttributeSettingsTests
{
    [Fact]
    public void UseDatabase_DefaultsToTrue()
    {
        PropertyAttribute attr = new();

        Assert.True(attr.UseDatabase);
    }

    [Fact]
    public void UseDatabase_CanBeSetToFalse()
    {
        PropertyAttribute attr = new() { UseDatabase = false };

        Assert.False(attr.UseDatabase);
    }

    [Fact]
    public void MaxStrategyRejections_DefaultsFive()
    {
        PropertyAttribute attr = new();

        Assert.Equal(5, attr.MaxStrategyRejections);
    }

    [Fact]
    public void MaxStrategyRejections_CanBeSet()
    {
        PropertyAttribute attr = new() { MaxStrategyRejections = 20 };

        Assert.Equal(20, attr.MaxStrategyRejections);
    }

    [Fact]
    public void DeadlineMs_DefaultsZero()
    {
        PropertyAttribute attr = new();

        Assert.Equal(0, attr.DeadlineMs);
    }

    [Fact]
    public void DeadlineMs_CanBeSet()
    {
        PropertyAttribute attr = new() { DeadlineMs = 500 };

        Assert.Equal(500, attr.DeadlineMs);
    }

    [Fact]
    public async Task TestRunner_Deadline_TerminatesLongRunningTest()
    {
        ConjectureSettings settings = new()
        {
            MaxExamples = 10_000,
            Deadline = TimeSpan.FromMilliseconds(50),
        };

        await Assert.ThrowsAsync<ConjectureException>(() =>
            TestRunner.Run(settings, data =>
            {
                _ = data.DrawInteger(0, 100);
                Thread.Sleep(10);
            }));
    }
}

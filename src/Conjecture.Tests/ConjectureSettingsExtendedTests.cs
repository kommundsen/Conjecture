// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Tests;

public class ConjectureSettingsExtendedTests
{
    [Fact]
    public void UseDatabase_Default_IsTrue()
    {
        var settings = new ConjectureSettings();
        Assert.True(settings.UseDatabase);
    }

    [Fact]
    public void Deadline_Default_IsNull()
    {
        var settings = new ConjectureSettings();
        Assert.Null(settings.Deadline);
    }

    [Fact]
    public void MaxStrategyRejections_Default_IsFive()
    {
        var settings = new ConjectureSettings();
        Assert.Equal(5, settings.MaxStrategyRejections);
    }

    [Fact]
    public void MaxUnsatisfiedRatio_Default_Is200()
    {
        var settings = new ConjectureSettings();
        Assert.Equal(200, settings.MaxUnsatisfiedRatio);
    }

    [Fact]
    public void DatabasePath_Default_IsConjectureExamples()
    {
        var settings = new ConjectureSettings();
        Assert.Equal(".conjecture/examples/", settings.DatabasePath);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void MaxStrategyRejections_NegativeValue_Throws(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConjectureSettings { MaxStrategyRejections = value });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void MaxUnsatisfiedRatio_NegativeValue_Throws(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConjectureSettings { MaxUnsatisfiedRatio = value });
    }
}
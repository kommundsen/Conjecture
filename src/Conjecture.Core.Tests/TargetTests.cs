// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests;

public class TargetTests
{
    [Fact]
    public async Task Maximize_WithinTestBody_RecordsObservation()
    {
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize(10.0);
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Maximize_WithCustomLabel_RecordsWithLabel()
    {
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize(10.0, "custom");
            Assert.Equal(10.0, data.Observations["custom"]);
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Maximize_DefaultLabel_RecordsAsDefault()
    {
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize(42.0);
            Assert.Equal(42.0, data.Observations["default"]);
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Minimize_NegatesValue()
    {
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Target.Minimize(10.0, "custom");
            Assert.Equal(-10.0, data.Observations["custom"]);
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public void Maximize_OutsideTestContext_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Target.Maximize(1.0));
        Assert.Contains("test", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Maximize_SameLabel_LastValueWins()
    {
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Target.Maximize(1.0, "x");
            Target.Maximize(2.0, "x");
            Assert.Equal(2.0, data.Observations["x"]);
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Maximize_NaN_Throws()
    {
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, _ =>
        {
            Assert.Throws<ArgumentException>(() => Target.Maximize(double.NaN));
        });

        Assert.True(result.Passed);
    }
}

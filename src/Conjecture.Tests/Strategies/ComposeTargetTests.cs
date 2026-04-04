// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.Strategies;

public class ComposeTargetTests
{
    [Fact]
    public async Task Compose_Target_RecordsObservation()
    {
        var strategy = Generate.Compose(ctx =>
        {
            var n = ctx.Generate(Generate.Integers<int>(0, 100));
            ctx.Target(n, "size");
            return n;
        });

        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            int value = strategy.Generate(data);
            Assert.True(data.Observations.ContainsKey("size"));
            Assert.Equal((double)value, data.Observations["size"]);
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Compose_Target_DefaultLabel()
    {
        var strategy = Generate.Compose(ctx =>
        {
            var n = ctx.Generate(Generate.Integers<int>(0, 100));
            ctx.Target(n);
            return n;
        });

        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            strategy.Generate(data);
            Assert.True(data.Observations.ContainsKey("default"));
        });

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Compose_Target_NaN_Throws()
    {
        var strategy = Generate.Compose(ctx =>
        {
            Assert.Throws<ArgumentException>(() => ctx.Target(double.NaN));
            return 0;
        });

        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            strategy.Generate(data);
        });

        Assert.True(result.Passed);
    }
}

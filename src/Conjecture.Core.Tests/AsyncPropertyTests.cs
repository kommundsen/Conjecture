// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests;

public class AsyncPropertyTests
{
    [Fact]
    public async Task TaskReturning_PassingProperty_Passes()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };

        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            int x = Generate.Integers<int>(0, 10).Generate(data);
            await Task.Yield();
            if (x < 0) { throw new Exception("impossible"); }
        });

        Assert.True(result.Passed);
        Assert.Null(result.Counterexample);
    }

    [Fact]
    public async Task TaskReturning_FailingProperty_ReturnsCounterexample()
    {
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };

        TestRunResult result = await TestRunner.RunAsync(settings, async data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            await Task.Yield();
            if (x > 5) { throw new Exception("too large"); }
        });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
    }

    [Fact]
    public async Task SyncTaskReturning_PassingProperty_Passes()
    {
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 2UL };

        TestRunResult result = await TestRunner.RunAsync(settings, data =>
        {
            int x = Generate.Integers<int>(0, 10).Generate(data);
            return x < 0 ? throw new Exception("impossible") : Task.CompletedTask;
        });

        Assert.True(result.Passed);
        Assert.Null(result.Counterexample);
    }

    [Fact]
    public async Task AsyncProperty_SameSeedAsSyncProperty_ProducesSameValues()
    {
        ulong seed = 7UL;
        List<int> asyncValues = [];
        List<int> syncValues = [];

        await TestRunner.RunAsync(new ConjectureSettings { MaxExamples = 30, Seed = seed }, async data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            asyncValues.Add(x);
            await Task.Yield();
        });

        await TestRunner.Run(new ConjectureSettings { MaxExamples = 30, Seed = seed }, data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            syncValues.Add(x);
        });

        Assert.Equal(syncValues, asyncValues);
    }

    [Fact]
    public async Task AsyncProperty_SameSeed_ProducesSameCounterexample()
    {
        ulong seed = 42UL;

        TestRunResult first = await TestRunner.RunAsync(new ConjectureSettings { MaxExamples = 100, Seed = seed }, async data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            await Task.Yield();
            if (x > 10) { throw new Exception("too large"); }
        });

        TestRunResult second = await TestRunner.RunAsync(new ConjectureSettings { MaxExamples = 100, Seed = seed }, async data =>
        {
            int x = Generate.Integers<int>(0, 100).Generate(data);
            await Task.Yield();
            if (x > 10) { throw new Exception("too large"); }
        });

        Assert.False(first.Passed);
        Assert.False(second.Passed);
        Assert.Equal(first.Counterexample, second.Counterexample);
    }
}
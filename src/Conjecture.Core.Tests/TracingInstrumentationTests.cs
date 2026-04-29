// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;
using System.Diagnostics;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests;

[CollectionDefinition("Sequential", DisableParallelization = true)]
public sealed class SequentialCollection;

[Collection("Sequential")]
public sealed class TracingInstrumentationTests
{
    private static ActivityListener CreateCollectingListener(ConcurrentBag<Activity> collected)
    {
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == "Conjecture.Core",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => collected.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task PassingProperty_RootActivity_IsNamedPropertyTest()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Assert.Contains(activities, a => a.OperationName == "PropertyTest" && a.Parent is null);
    }

    [Fact]
    public async Task PassingProperty_GenerationChildActivity_IsPresent()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Assert.Contains(activities, a => a.OperationName == "PropertyTest.Generation");
    }

    [Fact]
    public async Task FailingProperty_ShrinkingChildActivity_IsPresent()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x > 5) { throw new Exception("too large"); }
        });

        Assert.Contains(activities, a => a.OperationName == "PropertyTest.Shrinking");
    }

    [Fact]
    public async Task PassingPropertyWithTargeting_TargetingChildActivity_IsPresent()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 20, Seed = 1UL, Targeting = true };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            Target.Maximize(x, "x");
        });

        Assert.Contains(activities, a => a.OperationName == "PropertyTest.Targeting");
    }

    [Fact]
    public async Task PassingPropertyWithTargetingDisabled_TargetingChildActivity_IsAbsent()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL, Targeting = false };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Assert.DoesNotContain(activities, a => a.OperationName == "PropertyTest.Targeting");
    }

    [Fact]
    public async Task PassingProperty_RootActivity_CarriesSeedTag()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 42UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Activity? root = activities.FirstOrDefault(a => a.OperationName == "PropertyTest" && a.Parent is null);
        Assert.NotNull(root);
        Assert.NotNull(root.GetTagItem("conjecture.seed"));
    }

    [Fact]
    public async Task PassingProperty_RootActivity_CarriesMaxExamplesTag()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Activity? root = activities.FirstOrDefault(a => a.OperationName == "PropertyTest" && a.Parent is null);
        Assert.NotNull(root);
        Assert.Equal(10, root.GetTagItem("conjecture.max_examples"));
    }

    [Fact]
    public async Task FailingProperty_RootActivity_HasTestStatusFail()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x > 5) { throw new Exception("too large"); }
        });

        Activity? root = activities.FirstOrDefault(a => a.OperationName == "PropertyTest" && a.Parent is null);
        Assert.NotNull(root);
        Assert.Equal("fail", root.GetTagItem("test.status"));
    }

    [Fact]
    public async Task PassingProperty_RootActivity_DoesNotHaveTestStatusFail()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Activity? root = activities.FirstOrDefault(a => a.OperationName == "PropertyTest" && a.Parent is null);
        Assert.NotNull(root);
        Assert.NotEqual("fail", root.GetTagItem("test.status"));
    }

    [Fact]
    public async Task SettingsWithTestName_RootActivity_CarriesTestNameTag()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL, TestName = "MyTestMethod" };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Activity? root = activities.FirstOrDefault(a => a.OperationName == "PropertyTest" && a.Parent is null);
        Assert.NotNull(root);
        Assert.Equal("MyTestMethod", root.GetTagItem("test.name"));
    }

    [Fact]
    public async Task SettingsWithoutTestName_RootActivity_DoesNotCarryTestNameTag()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Activity? root = activities.FirstOrDefault(a => a.OperationName == "PropertyTest" && a.Parent is null);
        Assert.NotNull(root);
        Assert.Null(root.GetTagItem("test.name"));
    }

    [Fact]
    public async Task FailingProperty_ShrinkingActivity_IsChildOfRootActivity()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x > 5) { throw new Exception("too large"); }
        });

        Activity? root = activities.FirstOrDefault(a => a.OperationName == "PropertyTest" && a.Parent is null);
        Activity? shrinking = activities.FirstOrDefault(a => a.OperationName == "PropertyTest.Shrinking");
        Assert.NotNull(root);
        Assert.NotNull(shrinking);
        Assert.Equal(root.Id, shrinking.ParentId);
    }

    [Fact]
    public async Task PassingProperty_GenerationActivity_IsChildOfRootActivity()
    {
        ConcurrentBag<Activity> activities = [];
        using ActivityListener listener = CreateCollectingListener(activities);

        ConjectureSettings settings = new() { MaxExamples = 10, Seed = 1UL };
        await TestRunner.Run(settings, data =>
        {
            int x = Strategy.Integers<int>(0, 100).Generate(data);
            if (x < 0) { throw new Exception("impossible"); }
        });

        Activity? root = activities.FirstOrDefault(a => a.OperationName == "PropertyTest" && a.Parent is null);
        Activity? generation = activities.FirstOrDefault(a => a.OperationName == "PropertyTest.Generation");
        Assert.NotNull(root);
        Assert.NotNull(generation);
        Assert.Equal(root.Id, generation.ParentId);
    }
}
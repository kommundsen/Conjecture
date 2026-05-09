// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;
using System.Reflection;

using Conjecture.TestingPlatform.Internal;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Conjecture.TestingPlatform.Tests.Internal;

public class PropertyTestFrameworkTests
{
    private static PropertyTestFramework CreateFramework(params Type[] types)
    {
        IEnumerable<Assembly> assemblies = types.Select(static t => t.Assembly).Distinct();
        FakeServiceProvider serviceProvider = new();
        return new PropertyTestFramework(
            serviceProvider,
            assemblies,
            t => types.Contains(t));
    }

    // ---- Behaviour 1: zero-parameter passing method → single PassedTestNodeStateProperty ----

    [Fact]
    public async Task RunAsync_ZeroParameterMethodThatPasses_PublishesSinglePassedUpdate()
    {
        PropertyTestFramework framework = CreateFramework(typeof(ZeroParameterTestMethods));
        CapturingMessageBus bus = new();
        SessionUid session = new("test-session");

        await framework.RunAsync(bus, session);

        List<TestNodeUpdateMessage> updates = bus.Updates
            .Where(static u => NodeUidFor(u) == ZeroParameterTestMethods.PassingMethodUid)
            .ToList();

        Assert.Single(updates);
        Assert.True(updates[0].TestNode.Properties.Any<PassedTestNodeStateProperty>());
    }

    // ---- Behaviour 2: zero-parameter method that throws → single FailedTestNodeStateProperty ----

    [Fact]
    public async Task RunAsync_ZeroParameterMethodThatThrows_PublishesSingleFailedUpdate()
    {
        PropertyTestFramework framework = CreateFramework(typeof(ZeroParameterTestMethods));
        CapturingMessageBus bus = new();
        SessionUid session = new("test-session");

        await framework.RunAsync(bus, session);

        List<TestNodeUpdateMessage> updates = bus.Updates
            .Where(static u => NodeUidFor(u) == ZeroParameterTestMethods.ThrowingMethodUid)
            .ToList();

        Assert.Single(updates);
        Assert.True(updates[0].TestNode.Properties.Any<FailedTestNodeStateProperty>());
    }

    // ---- Behaviour 3: zero-parameter async method that passes → single PassedTestNodeStateProperty ----

    [Fact]
    public async Task RunAsync_ZeroParameterAsyncMethodThatPasses_PublishesSinglePassedUpdate()
    {
        PropertyTestFramework framework = CreateFramework(typeof(ZeroParameterTestMethods));
        CapturingMessageBus bus = new();
        SessionUid session = new("test-session");

        await framework.RunAsync(bus, session);

        List<TestNodeUpdateMessage> updates = bus.Updates
            .Where(static u => NodeUidFor(u) == ZeroParameterTestMethods.AsyncPassingMethodUid)
            .ToList();

        Assert.Single(updates);
        Assert.True(updates[0].TestNode.Properties.Any<PassedTestNodeStateProperty>());
    }

    // ---- Behaviour 4: zero-parameter async method that throws → single FailedTestNodeStateProperty ----

    [Fact]
    public async Task RunAsync_ZeroParameterAsyncMethodThatThrows_PublishesSingleFailedUpdate()
    {
        PropertyTestFramework framework = CreateFramework(typeof(ZeroParameterTestMethods));
        CapturingMessageBus bus = new();
        SessionUid session = new("test-session");

        await framework.RunAsync(bus, session);

        List<TestNodeUpdateMessage> updates = bus.Updates
            .Where(static u => NodeUidFor(u) == ZeroParameterTestMethods.AsyncThrowingMethodUid)
            .ToList();

        Assert.Single(updates);
        Assert.True(updates[0].TestNode.Properties.Any<FailedTestNodeStateProperty>());
    }

    // ---- Behaviour 6: only one TestNodeUpdateMessage published (not MaxExamples messages) ----

    [Fact]
    public async Task RunAsync_ZeroParameterMethod_PublishesExactlyOneUpdate()
    {
        PropertyTestFramework framework = CreateFramework(typeof(ZeroParameterOneMethodOnly));
        CapturingMessageBus bus = new();
        SessionUid session = new("test-session");

        await framework.RunAsync(bus, session);

        // All updates must come from the single zero-parameter method.
        // The framework must not publish MaxExamples (100) messages.
        Assert.Single(bus.Updates);
    }

    // ---- Behaviour 7: parameterised method still goes through TestRunner (MaxExamples updates expected) ----

    [Fact]
    public async Task RunAsync_ParameterisedMethod_PublishesMoreThanOneUpdate()
    {
        PropertyTestFramework framework = CreateFramework(typeof(ParameterisedTestMethods));
        CapturingMessageBus bus = new();
        SessionUid session = new("test-session");

        await framework.RunAsync(bus, session);

        // The TestRunner loop publishes the final result plus intermediate log messages.
        // The key invariant is that the framework does NOT take the zero-parameter
        // short-circuit path: the result node still has PassedTestNodeStateProperty,
        // and the total number of updates for this method exceeds 1 (result + logs).
        List<TestNodeUpdateMessage> updates = bus.Updates
            .Where(static u => NodeUidFor(u) == ParameterisedTestMethods.IntMethodUid)
            .ToList();

        Assert.True(updates.Count > 1);
        Assert.Contains(updates, static u => u.TestNode.Properties.Any<PassedTestNodeStateProperty>());
    }

    // ---- Behaviour 8: failure with ExportReproductionOnFailure → FileArtifactProperty on failure node ----

    [Fact]
    public async Task RunAsync_FailingPropertyWithExportReproductionOnFailure_AddsFileArtifactProperty()
    {
        string outputDir = ReproExportEnabledFixtureMethods.ReproOutputPath;
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        Directory.CreateDirectory(outputDir);
        try
        {
            PropertyTestFramework framework = CreateFramework(typeof(ReproExportEnabledFixtureMethods));
            CapturingMessageBus bus = new();
            SessionUid session = new("test-session-repro");

            await framework.RunAsync(bus, session);

            List<TestNodeUpdateMessage> updates = bus.Updates
                .Where(static u => NodeUidFor(u) == ReproExportEnabledFixtureMethods.AlwaysFailsUid)
                .ToList();

            TestNode failureNode = updates
                .First(static u => u.TestNode.Properties.Any<FailedTestNodeStateProperty>())
                .TestNode;

            FileArtifactProperty? artifact = failureNode.Properties.SingleOrDefault<FileArtifactProperty>();
            Assert.NotNull(artifact);
            Assert.True(File.Exists(artifact.FileInfo.FullName));
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }
        }
    }

    // ---- Behaviour 9: failure without ExportReproductionOnFailure → no FileArtifactProperty ----

    [Fact]
    public async Task RunAsync_FailingPropertyWithoutExportReproductionOnFailure_AddsNoFileArtifactProperty()
    {
        PropertyTestFramework framework = CreateFramework(typeof(ReproExportDisabledFixtureMethods));
        CapturingMessageBus bus = new();
        SessionUid session = new("test-session-no-repro");

        await framework.RunAsync(bus, session);

        List<TestNodeUpdateMessage> updates = bus.Updates
            .Where(static u => NodeUidFor(u) == ReproExportDisabledFixtureMethods.AlwaysFailsUid)
            .ToList();

        TestNode failureNode = updates
            .First(static u => u.TestNode.Properties.Any<FailedTestNodeStateProperty>())
            .TestNode;

        FileArtifactProperty? artifact = failureNode.Properties.SingleOrDefault<FileArtifactProperty>();
        Assert.Null(artifact);
    }

    // ---- Behaviour 10: repro write failure → failure node published, no FileArtifactProperty, no throw ----

    [Fact]
    public async Task RunAsync_ReproWriteFailure_PublishesFailureNodeWithoutFileArtifactPropertyAndDoesNotThrow()
    {
        // The fixture's ReproductionOutputPath is a file that already exists,
        // causing Directory.CreateDirectory to fail inside ReproFileBuilder.WriteToFile.
        string blockerFile = ReproExportBlockedFixtureMethods.ReproOutputPath;
        string? parentDir = Path.GetDirectoryName(blockerFile);
        if (parentDir is not null)
        {
            Directory.CreateDirectory(parentDir);
        }

        File.WriteAllText(blockerFile, "blocker");
        try
        {
            PropertyTestFramework framework = CreateFramework(typeof(ReproExportBlockedFixtureMethods));
            CapturingMessageBus bus = new();
            SessionUid session = new("test-session-repro-fail");

            Exception? thrown = await Record.ExceptionAsync(async () =>
                await framework.RunAsync(bus, session));

            Assert.Null(thrown);

            List<TestNodeUpdateMessage> updates = bus.Updates
                .Where(static u => NodeUidFor(u) == ReproExportBlockedFixtureMethods.AlwaysFailsUid)
                .ToList();

            TestNode failureNode = updates
                .First(static u => u.TestNode.Properties.Any<FailedTestNodeStateProperty>())
                .TestNode;

            FileArtifactProperty? artifact = failureNode.Properties.SingleOrDefault<FileArtifactProperty>();
            Assert.Null(artifact);
        }
        finally
        {
            if (File.Exists(blockerFile))
            {
                File.Delete(blockerFile);
            }
        }
    }

    private static string NodeUidFor(TestNodeUpdateMessage message)
    {
        return message.TestNode.Uid.Value;
    }

    // ---- Fake infrastructure ----

    private sealed class CapturingMessageBus : IMessageBus
    {
        private readonly ConcurrentBag<TestNodeUpdateMessage> updates = [];

        public List<TestNodeUpdateMessage> Updates => updates.ToList();

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            if (data is TestNodeUpdateMessage msg)
            {
                updates.Add(msg);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}

// ---- Test fixture classes discovered via reflection ----

internal static class ZeroParameterTestMethods
{
    // These are computed once so that tests can reference them without reflection boilerplate.
    // The UIDs must match what TestCaseHelper.ComputeTestId produces for each method.
    internal static readonly string PassingMethodUid =
        Conjecture.Core.Internal.TestCaseHelper.ComputeTestId(
            typeof(ZeroParameterTestMethods).GetMethod(nameof(Passes))!);

    internal static readonly string ThrowingMethodUid =
        Conjecture.Core.Internal.TestCaseHelper.ComputeTestId(
            typeof(ZeroParameterTestMethods).GetMethod(nameof(Throws))!);

    internal static readonly string AsyncPassingMethodUid =
        Conjecture.Core.Internal.TestCaseHelper.ComputeTestId(
            typeof(ZeroParameterTestMethods).GetMethod(nameof(PassesAsync))!);

    internal static readonly string AsyncThrowingMethodUid =
        Conjecture.Core.Internal.TestCaseHelper.ComputeTestId(
            typeof(ZeroParameterTestMethods).GetMethod(nameof(ThrowsAsync))!);

    [Conjecture.TestingPlatform.PropertyAttribute(MaxExamples = 100)]
    public static void Passes() { }

    [Conjecture.TestingPlatform.PropertyAttribute(MaxExamples = 100)]
    public static void Throws()
    {
        throw new InvalidOperationException("always fails");
    }

    [Conjecture.TestingPlatform.PropertyAttribute(MaxExamples = 100)]
    public static Task PassesAsync()
    {
        return Task.CompletedTask;
    }

    [Conjecture.TestingPlatform.PropertyAttribute(MaxExamples = 100)]
    public static Task ThrowsAsync()
    {
        return Task.FromException(new InvalidOperationException("async fail"));
    }
}

internal static class ZeroParameterOneMethodOnly
{
    [Conjecture.TestingPlatform.PropertyAttribute(MaxExamples = 100)]
    public static void OnlyMethod() { }
}

internal static class ParameterisedTestMethods
{
    internal static readonly string IntMethodUid =
        Conjecture.Core.Internal.TestCaseHelper.ComputeTestId(
            typeof(ParameterisedTestMethods).GetMethod(nameof(IntProperty))!);

#pragma warning disable IDE0060
    [Conjecture.TestingPlatform.PropertyAttribute(MaxExamples = 10)]
    public static void IntProperty(int x) { }
#pragma warning restore IDE0060
}

// Fixture: failing property with ExportReproductionOnFailure enabled.
// ReproOutputPath is a const so it can be used in the attribute and from tests.
internal static class ReproExportEnabledFixtureMethods
{
    internal const string ReproOutputPath = "obj/test-repro-export-enabled/";

    internal static readonly string AlwaysFailsUid =
        Conjecture.Core.Internal.TestCaseHelper.ComputeTestId(
            typeof(ReproExportEnabledFixtureMethods).GetMethod(nameof(AlwaysFails))!);

#pragma warning disable IDE0060
    [Conjecture.TestingPlatform.PropertyAttribute(
        MaxExamples = 10,
        Seed = 1UL,
        ExportReproductionOnFailure = true,
        ReproductionOutputPath = ReproOutputPath)]
    public static void AlwaysFails(int x)
    {
        throw new InvalidOperationException("always fails for repro export test");
    }
#pragma warning restore IDE0060
}

// Fixture: failing property with ExportReproductionOnFailure disabled (default).
internal static class ReproExportDisabledFixtureMethods
{
    internal static readonly string AlwaysFailsUid =
        Conjecture.Core.Internal.TestCaseHelper.ComputeTestId(
            typeof(ReproExportDisabledFixtureMethods).GetMethod(nameof(AlwaysFails))!);

#pragma warning disable IDE0060
    [Conjecture.TestingPlatform.PropertyAttribute(
        MaxExamples = 10,
        Seed = 1UL,
        ExportReproductionOnFailure = false)]
    public static void AlwaysFails(int x)
    {
        throw new InvalidOperationException("always fails for no-repro test");
    }
#pragma warning restore IDE0060
}

// Fixture: failing property whose ReproductionOutputPath is intentionally set to a file path
// (not a directory) so that the write will fail, exercising the swallow-on-failure branch.
internal static class ReproExportBlockedFixtureMethods
{
    internal const string ReproOutputPath = "obj/test-repro-blocked-sentinel.txt";

    internal static readonly string AlwaysFailsUid =
        Conjecture.Core.Internal.TestCaseHelper.ComputeTestId(
            typeof(ReproExportBlockedFixtureMethods).GetMethod(nameof(AlwaysFails))!);

#pragma warning disable IDE0060
    [Conjecture.TestingPlatform.PropertyAttribute(
        MaxExamples = 10,
        Seed = 1UL,
        ExportReproductionOnFailure = true,
        ReproductionOutputPath = ReproOutputPath)]
    public static void AlwaysFails(int x)
    {
        throw new InvalidOperationException("always fails for blocked-write test");
    }
#pragma warning restore IDE0060
}
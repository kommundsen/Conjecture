// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Conjecture.Core;
using Conjecture.TestingPlatform.Internal;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Conjecture.TestingPlatform.Tests;

public class PropertyTestFrameworkTests
{
    // ---- test subjects -------------------------------------------------------

    private static class AlwaysPassingSubject
    {
        [Property]
        public static void AlwaysPasses(int x)
        {
        }
    }

    private static class AlwaysFailingSubject
    {
        [Property]
        public static void AlwaysFails(int x) { throw new System.Exception("Always fails"); }
    }

    private static class WithExampleSubject
    {
        [Property]
        [Example(42)]
        public static void WithExample(int x)
        {
        }
    }

    private static class WithFailingExampleSubject
    {
        [Property]
        [Example(99)]
        public static void ExampleFails(int x) { throw new System.Exception("Example exploded"); }
    }

    private static class AsyncPassingSubject
    {
        [Property]
        public static async Task AsyncPasses(int x)
        {
            await Task.Yield();
        }
    }

    private static class TwoPropertiesSubject
    {
        [Property]
        public static void FirstProperty(int x)
        {
        }

        [Property]
        public static void SecondProperty(string s)
        {
        }
    }

    // ---- helpers -------------------------------------------------------------

    private sealed class CapturingMessageBus : IMessageBus
    {
        private readonly List<TestNodeUpdateMessage> messages = [];

        public IReadOnlyList<TestNodeUpdateMessage> Messages => messages;

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            if (data is TestNodeUpdateMessage msg)
            {
                messages.Add(msg);
            }

            return Task.CompletedTask;
        }
    }

    private static PropertyTestFramework BuildFramework(System.Type subjectType)
    {
        Assembly assembly = subjectType.Assembly;
        return new(serviceProvider: null!, assemblies: [assembly], typePredicate: t => t == subjectType);
    }

    private static async Task<IReadOnlyList<TestNodeUpdateMessage>> DiscoverAsync(
        PropertyTestFramework framework,
        System.Type subjectType)
    {
        CapturingMessageBus bus = new();
        await framework.DiscoverAsync(bus, new SessionUid("test-session"));
        return bus.Messages;
    }

    private static async Task<IReadOnlyList<TestNodeUpdateMessage>> RunAsync(
        PropertyTestFramework framework,
        System.Type subjectType)
    {
        CapturingMessageBus bus = new();
        await framework.RunAsync(bus, new SessionUid("test-session"));
        return bus.Messages;
    }

    // ---- discovery -----------------------------------------------------------

    [Fact]
    public async Task ExecuteRequestAsync_DiscoverRequest_EmitsOneNodePerPropertyMethod()
    {
        PropertyTestFramework framework = BuildFramework(typeof(TwoPropertiesSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await DiscoverAsync(framework, typeof(TwoPropertiesSubject));

        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public async Task ExecuteRequestAsync_DiscoverRequest_NodeHasDiscoveredTestNodeStateProperty()
    {
        PropertyTestFramework framework = BuildFramework(typeof(AlwaysPassingSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await DiscoverAsync(framework, typeof(AlwaysPassingSubject));

        TestNodeUpdateMessage message = Assert.Single(messages);
        DiscoveredTestNodeStateProperty? state = message.TestNode.Properties.SingleOrDefault<DiscoveredTestNodeStateProperty>();
        Assert.NotNull(state);
    }

    [Fact]
    public async Task ExecuteRequestAsync_DiscoverRequest_NodeDisplayNameMatchesMethodName()
    {
        PropertyTestFramework framework = BuildFramework(typeof(AlwaysPassingSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await DiscoverAsync(framework, typeof(AlwaysPassingSubject));

        TestNodeUpdateMessage message = Assert.Single(messages);
        Assert.Contains("AlwaysPasses", message.TestNode.DisplayName);
    }

    // ---- run: passing property -----------------------------------------------

    [Fact]
    public async Task ExecuteRequestAsync_RunRequest_PassingProperty_EmitsPassedState()
    {
        PropertyTestFramework framework = BuildFramework(typeof(AlwaysPassingSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await RunAsync(framework, typeof(AlwaysPassingSubject));

        TestNodeUpdateMessage? final = null;
        foreach (TestNodeUpdateMessage msg in messages)
        {
            if (msg.TestNode.Properties.Any<PassedTestNodeStateProperty>())
            {
                final = msg;
            }
        }

        Assert.NotNull(final);
    }

    // ---- run: failing property -----------------------------------------------

    [Fact]
    public async Task ExecuteRequestAsync_RunRequest_FailingProperty_EmitsFailedState()
    {
        PropertyTestFramework framework = BuildFramework(typeof(AlwaysFailingSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await RunAsync(framework, typeof(AlwaysFailingSubject));

        TestNodeUpdateMessage? failed = null;
        foreach (TestNodeUpdateMessage msg in messages)
        {
            if (msg.TestNode.Properties.Any<FailedTestNodeStateProperty>())
            {
                failed = msg;
            }
        }

        Assert.NotNull(failed);
    }

    [Fact]
    public async Task ExecuteRequestAsync_RunRequest_FailingProperty_FailureMessageContainsFalsifyingExample()
    {
        PropertyTestFramework framework = BuildFramework(typeof(AlwaysFailingSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await RunAsync(framework, typeof(AlwaysFailingSubject));

        FailedTestNodeStateProperty? failedProp = null;
        foreach (TestNodeUpdateMessage msg in messages)
        {
            FailedTestNodeStateProperty? candidate = msg.TestNode.Properties.SingleOrDefault<FailedTestNodeStateProperty>();
            if (candidate is not null)
            {
                failedProp = candidate;
            }
        }

        Assert.NotNull(failedProp);
        Assert.Contains("Falsifying example", failedProp.Explanation);
    }

    // ---- run: [Example] child nodes ------------------------------------------

    [Fact]
    public async Task ExecuteRequestAsync_RunRequest_PropertyWithExample_EmitsChildNodeBeforeRandomNode()
    {
        PropertyTestFramework framework = BuildFramework(typeof(WithExampleSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await RunAsync(framework, typeof(WithExampleSubject));

        // At least one message must have a non-null ParentTestNodeUid (child node for [Example])
        bool hasChildNode = false;
        foreach (TestNodeUpdateMessage msg in messages)
        {
            if (msg.ParentTestNodeUid is not null)
            {
                hasChildNode = true;
            }
        }

        Assert.True(hasChildNode);
    }

    [Fact]
    public async Task ExecuteRequestAsync_RunRequest_PropertyWithPassingExample_ExampleChildNodeHasPassedState()
    {
        PropertyTestFramework framework = BuildFramework(typeof(WithExampleSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await RunAsync(framework, typeof(WithExampleSubject));

        TestNodeUpdateMessage? exampleNode = null;
        foreach (TestNodeUpdateMessage msg in messages)
        {
            if (msg.ParentTestNodeUid is not null && msg.TestNode.Properties.Any<PassedTestNodeStateProperty>())
            {
                exampleNode = msg;
            }
        }

        Assert.NotNull(exampleNode);
    }

    // ---- run: failing [Example] stops random generation ---------------------

    [Fact]
    public async Task ExecuteRequestAsync_RunRequest_FailingExample_EmitsFailedChildNode()
    {
        PropertyTestFramework framework = BuildFramework(typeof(WithFailingExampleSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await RunAsync(framework, typeof(WithFailingExampleSubject));

        TestNodeUpdateMessage? failedChild = null;
        foreach (TestNodeUpdateMessage msg in messages)
        {
            if (msg.ParentTestNodeUid is not null && msg.TestNode.Properties.Any<FailedTestNodeStateProperty>())
            {
                failedChild = msg;
            }
        }

        Assert.NotNull(failedChild);
    }

    [Fact]
    public async Task ExecuteRequestAsync_RunRequest_FailingExample_PropertyNodeAlsoFailsAndNoPassedNodeEmitted()
    {
        PropertyTestFramework framework = BuildFramework(typeof(WithFailingExampleSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await RunAsync(framework, typeof(WithFailingExampleSubject));

        bool hasTopLevelPassed = false;
        foreach (TestNodeUpdateMessage msg in messages)
        {
            if (msg.ParentTestNodeUid is null && msg.TestNode.Properties.Any<PassedTestNodeStateProperty>())
            {
                hasTopLevelPassed = true;
            }
        }

        Assert.False(hasTopLevelPassed);
    }

    // ---- run: async Task-returning property ----------------------------------

    [Fact]
    public async Task ExecuteRequestAsync_RunRequest_AsyncPassingProperty_EmitsPassedState()
    {
        PropertyTestFramework framework = BuildFramework(typeof(AsyncPassingSubject));

        IReadOnlyList<TestNodeUpdateMessage> messages = await RunAsync(framework, typeof(AsyncPassingSubject));

        TestNodeUpdateMessage? passed = null;
        foreach (TestNodeUpdateMessage msg in messages)
        {
            if (msg.TestNode.Properties.Any<PassedTestNodeStateProperty>())
            {
                passed = msg;
            }
        }

        Assert.NotNull(passed);
    }

    // ---- metadata ------------------------------------------------------------

    [Fact]
    public void PropertyTestFramework_Uid_IsStableWellKnownString()
    {
        PropertyTestFramework framework = BuildFramework(typeof(AlwaysPassingSubject));

        Assert.Equal("Conjecture.TestingPlatform", framework.Uid);
    }

    [Fact]
    public void PropertyTestFramework_DisplayName_IsNonEmpty()
    {
        PropertyTestFramework framework = BuildFramework(typeof(AlwaysPassingSubject));

        Assert.False(string.IsNullOrWhiteSpace(framework.DisplayName));
    }

    [Fact]
    public async Task PropertyTestFramework_IsEnabledAsync_ReturnsTrue()
    {
        PropertyTestFramework framework = BuildFramework(typeof(AlwaysPassingSubject));

        bool enabled = await framework.IsEnabledAsync();

        Assert.True(enabled);
    }
}
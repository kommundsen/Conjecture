// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Conjecture.Core;
using Conjecture.TestingPlatform.Internal;

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Conjecture.TestingPlatform.Tests;

public class ExtensionCapabilityTests
{
    // ---- test subjects -------------------------------------------------------

    private static class AlwaysFailingSubject
    {
        [Property(MaxExamples = 1)]
        public static void AlwaysFails(int x) { throw new System.Exception("Always fails"); }
    }

    private static class AlwaysPassingSubject
    {
        [Property(MaxExamples = 1)]
        public static void AlwaysPasses(int x) { }
    }

    private static class ExampleFailingSubject
    {
        [Property]
        [Example(0)]
        public static void FailsOnExample(int x) { throw new System.Exception("Example failed"); }
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

    // ---- ITrxReportCapability membership ------------------------------------

    [Fact]
    public void PropertyTestFrameworkCapabilities_ImplementsITrxReportCapability()
    {
        PropertyTestFrameworkCapabilities capabilities = new();

        Assert.True(capabilities is ITrxReportCapability);
    }

    [Fact]
    public void PropertyTestFrameworkCapabilities_CapabilitiesCollection_ContainsTrxReportCapability()
    {
        PropertyTestFrameworkCapabilities capabilities = new();

        bool found = false;
        foreach (ITestFrameworkCapability cap in capabilities.Capabilities)
        {
            if (cap is ITrxReportCapability)
            {
                found = true;
            }
        }

        Assert.True(found);
    }

    // ---- IsSupported ---------------------------------------------------------

    [Fact]
    public void ITrxReportCapability_IsSupported_ReturnsTrue()
    {
        PropertyTestFrameworkCapabilities capabilities = new();
        ITrxReportCapability trx = (ITrxReportCapability)capabilities;

        Assert.True(trx.IsSupported);
    }

    // ---- TrxExceptionProperty only emitted after Enable() -------------------

    [Fact]
    public async Task RunAsync_WithoutEnablingTrx_FailedNode_DoesNotHaveTrxExceptionProperty()
    {
        PropertyTestFramework framework = BuildFramework(typeof(AlwaysFailingSubject));
        CapturingMessageBus bus = new();

        await framework.RunAsync(bus, new SessionUid("test-session"));

        bool hasTrxException = false;
        foreach (TestNodeUpdateMessage msg in bus.Messages)
        {
            if (msg.TestNode.Properties.SingleOrDefault<TrxExceptionProperty>() is not null)
            {
                hasTrxException = true;
            }
        }

        Assert.False(hasTrxException);
    }

    [Fact]
    public async Task RunAsync_AfterEnablingTrx_FailedNode_HasTrxExceptionPropertyWithCounterexampleMessage()
    {
        PropertyTestFrameworkCapabilities capabilities = new();
        ITrxReportCapability trx = (ITrxReportCapability)capabilities;
        trx.Enable();

        PropertyTestFramework framework = new(
            serviceProvider: null!,
            assemblies: [typeof(AlwaysFailingSubject).Assembly],
            typePredicate: t => t == typeof(AlwaysFailingSubject),
            capabilities: capabilities);
        CapturingMessageBus bus = new();

        await framework.RunAsync(bus, new SessionUid("test-session"));

        TrxExceptionProperty? trxException = null;
        foreach (TestNodeUpdateMessage msg in bus.Messages)
        {
            TrxExceptionProperty? candidate = msg.TestNode.Properties.SingleOrDefault<TrxExceptionProperty>();
            if (candidate is not null && msg.TestNode.Properties.SingleOrDefault<FailedTestNodeStateProperty>() is not null)
            {
                trxException = candidate;
            }
        }

        Assert.NotNull(trxException);
        Assert.False(string.IsNullOrWhiteSpace(trxException.Message));
    }

    [Fact]
    public async Task RunAsync_AfterEnablingTrx_ExampleFailure_HasTrxExceptionProperty()
    {
        PropertyTestFrameworkCapabilities capabilities = new();
        ITrxReportCapability trx = (ITrxReportCapability)capabilities;
        trx.Enable();

        PropertyTestFramework framework = new(
            serviceProvider: null!,
            assemblies: [typeof(ExampleFailingSubject).Assembly],
            typePredicate: t => t == typeof(ExampleFailingSubject),
            capabilities: capabilities);
        CapturingMessageBus bus = new();

        await framework.RunAsync(bus, new SessionUid("test-session"));

        TrxExceptionProperty? trxException = null;
        foreach (TestNodeUpdateMessage msg in bus.Messages)
        {
            TrxExceptionProperty? candidate = msg.TestNode.Properties.SingleOrDefault<TrxExceptionProperty>();
            if (candidate is not null && msg.TestNode.Properties.SingleOrDefault<FailedTestNodeStateProperty>() is not null)
            {
                trxException = candidate;
            }
        }

        Assert.NotNull(trxException);
        Assert.False(string.IsNullOrWhiteSpace(trxException.Message));
    }

    [Fact]
    public async Task RunAsync_WithoutEnablingTrx_ExampleFailure_DoesNotHaveTrxExceptionProperty()
    {
        PropertyTestFramework framework = BuildFramework(typeof(ExampleFailingSubject));
        CapturingMessageBus bus = new();

        await framework.RunAsync(bus, new SessionUid("test-session"));

        bool hasTrxException = false;
        foreach (TestNodeUpdateMessage msg in bus.Messages)
        {
            if (msg.TestNode.Properties.SingleOrDefault<TrxExceptionProperty>() is not null)
            {
                hasTrxException = true;
            }
        }

        Assert.False(hasTrxException);
    }

    [Fact]
    public async Task RunAsync_AfterEnablingTrx_PassedNode_DoesNotHaveTrxExceptionProperty()
    {
        PropertyTestFrameworkCapabilities capabilities = new();
        ITrxReportCapability trx = (ITrxReportCapability)capabilities;
        trx.Enable();

        PropertyTestFramework framework = new(
            serviceProvider: null!,
            assemblies: [typeof(AlwaysPassingSubject).Assembly],
            typePredicate: t => t == typeof(AlwaysPassingSubject),
            capabilities: capabilities);
        CapturingMessageBus bus = new();

        await framework.RunAsync(bus, new SessionUid("test-session"));

        bool hasTrxException = false;
        foreach (TestNodeUpdateMessage msg in bus.Messages)
        {
            if (msg.TestNode.Properties.SingleOrDefault<TrxExceptionProperty>() is not null)
            {
                hasTrxException = true;
            }
        }

        Assert.False(hasTrxException);
    }
}
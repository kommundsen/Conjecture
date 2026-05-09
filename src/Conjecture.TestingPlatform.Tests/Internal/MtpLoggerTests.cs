// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;
using System.Threading.Tasks;

using Conjecture.TestingPlatform.Internal;

using Microsoft.Extensions.Logging;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;

using Xunit;

namespace Conjecture.TestingPlatform.Tests.Internal;

public class MtpLoggerTests
{
    // ── Log produces TestMetadataProperty ────────────────────────────────────

    [Fact]
    public void Log_InformationLevel_PublishesTestMetadataPropertyWithExpectedKey()
    {
        CapturingMessageBus bus = new();
        TestNodeUid nodeUid = new("test-node");
        SessionUid session = new("test-session");
        MtpLogger logger = new(bus, nodeUid, session);

        logger.Log(LogLevel.Information, default, "hello", null, static (s, _) => s);

        TestNodeUpdateMessage message = Assert.Single(bus.Updates);
        TestMetadataProperty? property = message.TestNode.Properties
            .SingleOrDefault<TestMetadataProperty>();
        Assert.NotNull(property);
        Assert.Equal($"conjecture.log.{LogLevel.Information}", property.Key);
        Assert.Equal("hello", property.Value);
    }

    [Fact]
    public void Log_WarningLevel_PublishesTestMetadataPropertyWithWarningKey()
    {
        CapturingMessageBus bus = new();
        TestNodeUid nodeUid = new("test-node");
        SessionUid session = new("test-session");
        MtpLogger logger = new(bus, nodeUid, session);

        logger.Log(LogLevel.Warning, default, "watch out", null, static (s, _) => s);

        TestNodeUpdateMessage message = Assert.Single(bus.Updates);
        TestMetadataProperty? property = message.TestNode.Properties
            .SingleOrDefault<TestMetadataProperty>();
        Assert.NotNull(property);
        Assert.Equal($"conjecture.log.{LogLevel.Warning}", property.Key);
    }

    [Fact]
    public void Log_BelowInformationLevel_DoesNotPublish()
    {
        CapturingMessageBus bus = new();
        TestNodeUid nodeUid = new("test-node");
        SessionUid session = new("test-session");
        MtpLogger logger = new(bus, nodeUid, session);

        logger.Log(LogLevel.Debug, default, "verbose", null, static (s, _) => s);

        Assert.Empty(bus.Updates);
    }

    // ── Fake infrastructure ───────────────────────────────────────────────────

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
}
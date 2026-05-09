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
    // ── Log_Information_Publishes_StandardOutputProperty_With_Level_Prefix ───

    [Fact]
    public void Log_Information_Publishes_StandardOutputProperty_With_Level_Prefix()
    {
        CapturingMessageBus bus = new();
        TestNodeUid nodeUid = new("test-node");
        SessionUid session = new("test-session");
        MtpLogger logger = new(bus, nodeUid, session);

        logger.Log(LogLevel.Information, default, "hello", null, static (s, _) => s);

        TestNodeUpdateMessage message = Assert.Single(bus.Updates);
        StandardOutputProperty? property = message.TestNode.Properties
            .SingleOrDefault<StandardOutputProperty>();
        Assert.NotNull(property);
        Assert.StartsWith("[Information] ", property.StandardOutput);
        Assert.EndsWith(Environment.NewLine, property.StandardOutput);
        Assert.Contains("hello", property.StandardOutput);
    }

    // ── Log_Error_Publishes_StandardErrorProperty ─────────────────────────────

    [Fact]
    public void Log_Error_Publishes_StandardErrorProperty()
    {
        CapturingMessageBus bus = new();
        TestNodeUid nodeUid = new("test-node");
        SessionUid session = new("test-session");
        MtpLogger logger = new(bus, nodeUid, session);

        logger.Log(LogLevel.Error, default, "bad thing", null, static (s, _) => s);

        TestNodeUpdateMessage message = Assert.Single(bus.Updates);
        StandardErrorProperty? property = message.TestNode.Properties
            .SingleOrDefault<StandardErrorProperty>();
        Assert.NotNull(property);
        Assert.Contains("bad thing", property.StandardError);
        Assert.Null(message.TestNode.Properties.SingleOrDefault<StandardOutputProperty>());
    }

    // ── Log_Critical_Routed_To_StandardError ─────────────────────────────────

    [Fact]
    public void Log_Critical_Routed_To_StandardError()
    {
        CapturingMessageBus bus = new();
        TestNodeUid nodeUid = new("test-node");
        SessionUid session = new("test-session");
        MtpLogger logger = new(bus, nodeUid, session);

        logger.Log(LogLevel.Critical, default, "fatal", null, static (s, _) => s);

        TestNodeUpdateMessage message = Assert.Single(bus.Updates);
        StandardErrorProperty? property = message.TestNode.Properties
            .SingleOrDefault<StandardErrorProperty>();
        Assert.NotNull(property);
        Assert.Contains("fatal", property.StandardError);
        Assert.Null(message.TestNode.Properties.SingleOrDefault<StandardOutputProperty>());
    }

    // ── Log_Below_Information_Threshold_Publishes_Nothing ────────────────────

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    public void Log_Below_Information_Threshold_Publishes_Nothing(LogLevel level)
    {
        CapturingMessageBus bus = new();
        TestNodeUid nodeUid = new("test-node");
        SessionUid session = new("test-session");
        MtpLogger logger = new(bus, nodeUid, session);

        logger.Log(level, default, "verbose", null, static (s, _) => s);

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

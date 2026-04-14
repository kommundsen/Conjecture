// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Conjecture.TestingPlatform.Internal;

using Microsoft.Extensions.Logging;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Conjecture.TestingPlatform.Tests;

public class MtpLoggerTests
{
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

    private static MtpLogger BuildLogger(CapturingMessageBus bus)
    {
        TestNodeUid nodeUid = new("test-node");
        SessionUid session = new("test-session");
        return new(bus, nodeUid, session);
    }

    // ---- IsEnabled -----------------------------------------------------------

    [Fact]
    public void IsEnabled_InformationLevel_ReturnsTrue()
    {
        CapturingMessageBus bus = new();
        MtpLogger logger = BuildLogger(bus);

        bool result = logger.IsEnabled(LogLevel.Information);

        Assert.True(result);
    }

    [Fact]
    public void IsEnabled_WarningLevel_ReturnsTrue()
    {
        CapturingMessageBus bus = new();
        MtpLogger logger = BuildLogger(bus);

        bool result = logger.IsEnabled(LogLevel.Warning);

        Assert.True(result);
    }

    [Fact]
    public void IsEnabled_ErrorLevel_ReturnsTrue()
    {
        CapturingMessageBus bus = new();
        MtpLogger logger = BuildLogger(bus);

        bool result = logger.IsEnabled(LogLevel.Error);

        Assert.True(result);
    }

    [Fact]
    public void IsEnabled_DebugLevel_ReturnsFalse()
    {
        CapturingMessageBus bus = new();
        MtpLogger logger = BuildLogger(bus);

        bool result = logger.IsEnabled(LogLevel.Debug);

        Assert.False(result);
    }

    [Fact]
    public void IsEnabled_TraceLevel_ReturnsFalse()
    {
        CapturingMessageBus bus = new();
        MtpLogger logger = BuildLogger(bus);

        bool result = logger.IsEnabled(LogLevel.Trace);

        Assert.False(result);
    }

    // ---- Log: messages published or suppressed --------------------------------

    [Theory]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void Log_AtOrAboveInformation_PublishesOneMessage(LogLevel level)
    {
        CapturingMessageBus bus = new();
        MtpLogger logger = BuildLogger(bus);

        logger.Log(level, new EventId(0), "hello", null, static (s, _) => s);

        Assert.Single(bus.Messages);
    }

    [Theory]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Trace)]
    public void Log_BelowInformation_PublishesNothing(LogLevel level)
    {
        CapturingMessageBus bus = new();
        MtpLogger logger = BuildLogger(bus);

        logger.Log(level, new EventId(0), "suppressed", null, static (s, _) => s);

        Assert.Empty(bus.Messages);
    }

    // ---- Log: property key format --------------------------------------------

    [Theory]
    [InlineData(LogLevel.Information, "conjecture.log.Information")]
    [InlineData(LogLevel.Warning, "conjecture.log.Warning")]
    [InlineData(LogLevel.Error, "conjecture.log.Error")]
    [InlineData(LogLevel.Critical, "conjecture.log.Critical")]
    public void Log_AtOrAboveInformation_NodeHasKeyValuePropertyWithCorrectKey(
        LogLevel level, string expectedKey)
    {
        CapturingMessageBus bus = new();
        MtpLogger logger = BuildLogger(bus);

        logger.Log(level, new EventId(0), "msg", null, static (s, _) => s);

        TestNodeUpdateMessage message = Assert.Single(bus.Messages);
#pragma warning disable CS0618
        KeyValuePairStringProperty? prop =
            message.TestNode.Properties.SingleOrDefault<KeyValuePairStringProperty>();
#pragma warning restore CS0618
        Assert.NotNull(prop);
        Assert.Equal(expectedKey, prop.Key);
    }

    // ---- Log: property value is formatted message ----------------------------

    [Fact]
    public void Log_InformationMessage_NodePropertyValueIsFormattedText()
    {
        CapturingMessageBus bus = new();
        MtpLogger logger = BuildLogger(bus);

        logger.Log(
            LogLevel.Information,
            new EventId(0),
            "hello world",
            null,
            static (s, _) => s);

        TestNodeUpdateMessage message = Assert.Single(bus.Messages);
#pragma warning disable CS0618
        KeyValuePairStringProperty? prop =
            message.TestNode.Properties.SingleOrDefault<KeyValuePairStringProperty>();
#pragma warning restore CS0618
        Assert.NotNull(prop);
        Assert.Equal("hello world", prop.Value);
    }

    // ---- Log: message carries correct node uid and session uid ---------------

    [Fact]
    public void Log_Information_PublishedMessageCarriesNodeUid()
    {
        CapturingMessageBus bus = new();
        TestNodeUid nodeUid = new("my-node");
        SessionUid session = new("my-session");
        MtpLogger logger = new(bus, nodeUid, session);

        logger.Log(LogLevel.Information, new EventId(0), "test", null, static (s, _) => s);

        TestNodeUpdateMessage message = Assert.Single(bus.Messages);
        Assert.Equal("my-node", message.TestNode.Uid.Value);
    }

    [Fact]
    public void Log_Information_PublishedMessageCarriesSessionUid()
    {
        CapturingMessageBus bus = new();
        TestNodeUid nodeUid = new("my-node");
        SessionUid session = new("my-session");
        MtpLogger logger = new(bus, nodeUid, session);

        logger.Log(LogLevel.Information, new EventId(0), "test", null, static (s, _) => s);

        TestNodeUpdateMessage message = Assert.Single(bus.Messages);
        Assert.Equal("my-session", message.SessionUid.Value);
    }

    // ---- BeginScope ----------------------------------------------------------

    [Fact]
    public void BeginScope_AnyState_ReturnsNull()
    {
        CapturingMessageBus bus = new();
        MtpLogger logger = BuildLogger(bus);

        IDisposable? scope = logger.BeginScope("some-scope");

        Assert.Null(scope);
    }
}
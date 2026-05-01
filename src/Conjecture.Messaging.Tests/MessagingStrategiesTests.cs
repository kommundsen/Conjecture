// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Core;
using Conjecture.Messaging;

namespace Conjecture.Messaging.Tests;

public class MessagingStrategiesTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>(0);

    [Fact]
    public void Messaging_ReturnsMessagingGenerateBuilder()
    {
        MessagingStrategies builder = Strategy.Messaging;

        Assert.NotNull(builder);
    }

    [Fact]
    public void Publish_ReturnsStrategyOfMessageInteraction()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Publish(
            "orders",
            Strategy.Arrays(Strategy.Integers<byte>(), 8, 8).Select(static b => (ReadOnlyMemory<byte>)b));

        Assert.NotNull(strategy);
    }

    [Fact]
    public void Publish_GeneratedValue_HasCorrectDestination()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Publish(
            "orders",
            Strategy.Arrays(Strategy.Integers<byte>(), 4, 4).Select(static b => (ReadOnlyMemory<byte>)b));

        MessageInteraction sample = strategy.WithSeed(1UL).Sample();

        Assert.Equal("orders", sample.Destination);
    }

    [Fact]
    public void Publish_GeneratedValue_HasNonEmptyMessageId()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Publish(
            "orders",
            Strategy.Arrays(Strategy.Integers<byte>(), 4, 4).Select(static b => (ReadOnlyMemory<byte>)b));

        MessageInteraction sample = strategy.WithSeed(1UL).Sample();

        Assert.False(string.IsNullOrEmpty(sample.MessageId));
    }

    [Fact]
    public void Publish_SameSeed_ProducesSameMessageId()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Publish(
            "invoices",
            Strategy.Arrays(Strategy.Integers<byte>(), 4, 4).Select(static b => (ReadOnlyMemory<byte>)b));

        MessageInteraction first = strategy.WithSeed(42UL).Sample();
        MessageInteraction second = strategy.WithSeed(42UL).Sample();

        Assert.Equal(first.MessageId, second.MessageId);
    }

    [Fact]
    public void Publish_DifferentSeeds_LikelyProduceDifferentMessageIds()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Publish(
            "invoices",
            Strategy.Arrays(Strategy.Integers<byte>(), 4, 4).Select(static b => (ReadOnlyMemory<byte>)b));

        MessageInteraction first = strategy.WithSeed(1UL).Sample();
        MessageInteraction second = strategy.WithSeed(2UL).Sample();

        Assert.NotEqual(first.MessageId, second.MessageId);
    }

    [Fact]
    public void Publish_DefaultHeaders_AreEmpty()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Publish(
            "orders",
            Strategy.Arrays(Strategy.Integers<byte>(), 4, 4).Select(static b => (ReadOnlyMemory<byte>)b));

        MessageInteraction sample = strategy.WithSeed(1UL).Sample();

        Assert.Empty(sample.Headers);
    }

    [Fact]
    public void Publish_DefaultCorrelationId_IsNull()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Publish(
            "orders",
            Strategy.Arrays(Strategy.Integers<byte>(), 4, 4).Select(static b => (ReadOnlyMemory<byte>)b));

        MessageInteraction sample = strategy.WithSeed(1UL).Sample();

        Assert.Null(sample.CorrelationId);
    }

    [Fact]
    public void Consume_ReturnsStrategyOfMessageInteraction()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Consume("orders");

        Assert.NotNull(strategy);
    }

    [Fact]
    public void Consume_GeneratedValue_HasCorrectDestination()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Consume("orders");

        MessageInteraction sample = strategy.WithSeed(1UL).Sample();

        Assert.Equal("orders", sample.Destination);
    }

    [Fact]
    public void Consume_GeneratedValue_HasEmptyBody()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Consume("orders");

        MessageInteraction sample = strategy.WithSeed(1UL).Sample();

        Assert.Equal(0, sample.Body.Length);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("topic/sub-topic")]
    [InlineData("my-queue")]
    public void Publish_VariousDestinations_DestinationPreserved(string destination)
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Publish(
            destination,
            Strategy.Arrays(Strategy.Integers<byte>(), 2, 2).Select(static b => (ReadOnlyMemory<byte>)b));

        MessageInteraction sample = strategy.WithSeed(1UL).Sample();

        Assert.Equal(destination, sample.Destination);
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("topic/sub-topic")]
    [InlineData("my-queue")]
    public void Consume_VariousDestinations_DestinationPreserved(string destination)
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Consume(destination);

        MessageInteraction sample = strategy.WithSeed(1UL).Sample();

        Assert.Equal(destination, sample.Destination);
    }

    [Fact]
    public async Task Publish_RoundTrip_ViaInMemoryMessageBusTarget()
    {
        Strategy<MessageInteraction> strategy = Strategy.Messaging.Publish(
            "orders",
            Strategy.Arrays(Strategy.Integers<byte>(), 8, 8).Select(static b => (ReadOnlyMemory<byte>)b));

        MessageInteraction sent = strategy.WithSeed(77UL).Sample();
        InMemoryMessageBusTarget target = new();

        await target.ExecuteAsync(sent, CancellationToken.None);
        MessageInteraction? received = await target.ReceiveAsync("orders", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(sent, received);
    }

    [Fact]
    public void Publish_FullOverload_WithCustomHeadersAndCorrelationId()
    {
        Strategy<IReadOnlyDictionary<string, string>> headersStrategy =
            Strategy.Just<IReadOnlyDictionary<string, string>>(new Dictionary<string, string> { ["content-type"] = "application/json" });
        Strategy<string?> correlationStrategy = Strategy.Just<string?>("corr-abc");

        Strategy<MessageInteraction> strategy = Strategy.Messaging.Publish(
            "events",
            Strategy.Arrays(Strategy.Integers<byte>(), 4, 4).Select(static b => (ReadOnlyMemory<byte>)b),
            headersStrategy,
            correlationStrategy);

        MessageInteraction sample = strategy.WithSeed(1UL).Sample();

        Assert.Equal("events", sample.Destination);
        Assert.Equal("application/json", sample.Headers["content-type"]);
        Assert.Equal("corr-abc", sample.CorrelationId);
    }
}
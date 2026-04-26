// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Messaging;
using Conjecture.Messaging.RabbitMq;

namespace Conjecture.Messaging.RabbitMq.Tests;

/// <summary>
/// Unit tier — no Docker, no network.
/// All broker SDK calls go through hand-rolled fakes that implement the
/// IRabbitMqConnectionAdapter / IRabbitMqChannelAdapter seam.
/// </summary>
public class RabbitMqTargetTests : IAsyncDisposable
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(0);

    private readonly FakeRabbitMqConnectionAdapter fakeConnection = new();
    private readonly RabbitMqTarget sut;

    public RabbitMqTargetTests()
    {
        sut = new(fakeConnection);
    }

    public async ValueTask DisposeAsync()
    {
        await sut.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // ExecuteAsync — publish translation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SetsMessageId_FromMessageInteraction()
    {
        MessageInteraction interaction = new("orders", new byte[] { 1 }, EmptyHeaders, "msg-42");

        await sut.ExecuteAsync(interaction, CancellationToken.None);

        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("orders");
        Assert.Equal("msg-42", channel.LastPublished!.MessageId);
    }

    [Fact]
    public async Task ExecuteAsync_SetsBody_FromMessageInteractionBodyBytes()
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"value\":1}");
        MessageInteraction interaction = new("orders", body, EmptyHeaders, "msg-body");

        await sut.ExecuteAsync(interaction, CancellationToken.None);

        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("orders");
        Assert.Equal(body, channel.LastPublished!.Body.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_SetsHeaders_FromMessageInteractionHeaders()
    {
        Dictionary<string, string> headers = new()
        {
            ["content-type"] = "application/json",
            ["x-custom"] = "hello",
        };
        MessageInteraction interaction = new("orders", ReadOnlyMemory<byte>.Empty, headers, "msg-headers");

        await sut.ExecuteAsync(interaction, CancellationToken.None);

        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("orders");
        // RabbitMQ encodes string header values as UTF-8 byte[]; the fake stores them as byte[] too.
        byte[] contentTypeBytes = (byte[])channel.LastPublished!.Headers["content-type"];
        byte[] customBytes = (byte[])channel.LastPublished!.Headers["x-custom"];
        Assert.Equal("application/json", Encoding.UTF8.GetString(contentTypeBytes));
        Assert.Equal("hello", Encoding.UTF8.GetString(customBytes));
    }

    [Fact]
    public async Task ExecuteAsync_SetsCorrelationId_WhenPresent()
    {
        MessageInteraction interaction = new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "msg-corr", "corr-99");

        await sut.ExecuteAsync(interaction, CancellationToken.None);

        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("orders");
        Assert.Equal("corr-99", channel.LastPublished!.CorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_NullCorrelationId_LeavesCorrelationIdUnset()
    {
        MessageInteraction interaction = new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "msg-nocorr");

        await sut.ExecuteAsync(interaction, CancellationToken.None);

        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("orders");
        Assert.Null(channel.LastPublished!.CorrelationId);
    }

    // -----------------------------------------------------------------------
    // ReceiveAsync — receive translation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReceiveAsync_TranslatesMessageId_FromReceivedMessage()
    {
        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("events");
        channel.Enqueue(1UL, "msg-rx", ReadOnlyMemory<byte>.Empty, new Dictionary<string, object>(0), null);

        MessageInteraction? result = await sut.ReceiveAsync("events", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("msg-rx", result.MessageId);
    }

    [Fact]
    public async Task ReceiveAsync_TranslatesBody_FromReceivedMessage()
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"x\":2}");
        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("events");
        channel.Enqueue(2UL, "msg-rxbody", body, new Dictionary<string, object>(0), null);

        MessageInteraction? result = await sut.ReceiveAsync("events", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(body, result.Body.ToArray());
    }

    [Fact]
    public async Task ReceiveAsync_TranslatesHeaders_ToStringValues()
    {
        // RabbitMQ stores string header values as byte[]; the target must decode them back to string.
        byte[] valueBytes = Encoding.UTF8.GetBytes("application/json");
        Dictionary<string, object> rawHeaders = new() { ["content-type"] = valueBytes };
        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("events");
        channel.Enqueue(3UL, "msg-rxhdrs", ReadOnlyMemory<byte>.Empty, rawHeaders, null);

        MessageInteraction? result = await sut.ReceiveAsync("events", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("application/json", result.Headers["content-type"]);
    }

    [Fact]
    public async Task ReceiveAsync_TranslatesCorrelationId_FromReceivedMessage()
    {
        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("events");
        channel.Enqueue(4UL, "msg-rxcorr", ReadOnlyMemory<byte>.Empty, new Dictionary<string, object>(0), "corr-rx");

        MessageInteraction? result = await sut.ReceiveAsync("events", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("corr-rx", result.CorrelationId);
    }

    [Fact]
    public async Task ReceiveAsync_ReturnsNull_WhenQueueIsEmpty()
    {
        // fake channel has nothing queued — simulates BasicGet returning null
        MessageInteraction? result = await sut.ReceiveAsync("empty-q", TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // AcknowledgeAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AcknowledgeAsync_CallsBasicAck_OnChannel()
    {
        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("ack-q");
        channel.Enqueue(10UL, "msg-ack", ReadOnlyMemory<byte>.Empty, new Dictionary<string, object>(0), null);

        MessageInteraction? received = await sut.ReceiveAsync("ack-q", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(received);
        await sut.AcknowledgeAsync(received, CancellationToken.None);

        Assert.Equal(1, channel.AckCount);
    }

    // -----------------------------------------------------------------------
    // RejectAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RejectAsync_Requeue_True_CallsBasicNack_WithRequeueTrue()
    {
        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("rej-q");
        channel.Enqueue(20UL, "msg-nack-requeue", ReadOnlyMemory<byte>.Empty, new Dictionary<string, object>(0), null);

        MessageInteraction? received = await sut.ReceiveAsync("rej-q", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(received);
        await sut.RejectAsync(received, requeue: true, CancellationToken.None);

        Assert.Equal(1, channel.NackCount);
        Assert.True(channel.LastNackRequeue);
    }

    [Fact]
    public async Task RejectAsync_Requeue_False_CallsBasicNack_WithRequeueFalse()
    {
        FakeRabbitMqChannelAdapter channel = fakeConnection.GetChannel("dl-q");
        channel.Enqueue(21UL, "msg-nack-drop", ReadOnlyMemory<byte>.Empty, new Dictionary<string, object>(0), null);

        MessageInteraction? received = await sut.ReceiveAsync("dl-q", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(received);
        await sut.RejectAsync(received, requeue: false, CancellationToken.None);

        Assert.Equal(1, channel.NackCount);
        Assert.False(channel.LastNackRequeue);
    }

    // -----------------------------------------------------------------------
    // Lazy creation and caching — channels per destination
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Channel_IsLazilyCreated_OnFirstExecuteAsync()
    {
        Assert.Equal(0, fakeConnection.ChannelCreationCount("orders"));

        await sut.ExecuteAsync(new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "m1"), CancellationToken.None);

        Assert.Equal(1, fakeConnection.ChannelCreationCount("orders"));
    }

    [Fact]
    public async Task Channel_IsCached_AcrossMultiplePublishesToSameDestination()
    {
        await sut.ExecuteAsync(new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "m1"), CancellationToken.None);
        await sut.ExecuteAsync(new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "m2"), CancellationToken.None);

        Assert.Equal(1, fakeConnection.ChannelCreationCount("orders"));
    }

    [Fact]
    public async Task Channel_IsLazilyCreated_OnFirstReceiveAsync()
    {
        Assert.Equal(0, fakeConnection.ChannelCreationCount("items"));

        await sut.ReceiveAsync("items", TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.Equal(1, fakeConnection.ChannelCreationCount("items"));
    }

    [Fact]
    public async Task Channel_IsCached_AcrossMultipleReceiveCalls()
    {
        await sut.ReceiveAsync("items", TimeSpan.FromMilliseconds(10), CancellationToken.None);
        await sut.ReceiveAsync("items", TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.Equal(1, fakeConnection.ChannelCreationCount("items"));
    }

    // -----------------------------------------------------------------------
    // DisposeAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_DisposesAllCachedChannels()
    {
        RabbitMqTarget target = new(fakeConnection);
        await target.ExecuteAsync(new("dest-a", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "mx"), CancellationToken.None);
        await target.ExecuteAsync(new("dest-b", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "my"), CancellationToken.None);

        await target.DisposeAsync();

        Assert.True(fakeConnection.GetChannel("dest-a").IsDisposed);
        Assert.True(fakeConnection.GetChannel("dest-b").IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_DisposesChannelsCreatedForReceive()
    {
        RabbitMqTarget target = new(fakeConnection);
        await target.ReceiveAsync("rx-a", TimeSpan.FromMilliseconds(10), CancellationToken.None);
        await target.ReceiveAsync("rx-b", TimeSpan.FromMilliseconds(10), CancellationToken.None);

        await target.DisposeAsync();

        Assert.True(fakeConnection.GetChannel("rx-a").IsDisposed);
        Assert.True(fakeConnection.GetChannel("rx-b").IsDisposed);
    }
}
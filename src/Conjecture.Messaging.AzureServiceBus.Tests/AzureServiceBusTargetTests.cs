// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Messaging;
using Conjecture.Messaging.AzureServiceBus;

namespace Conjecture.Messaging.AzureServiceBus.Tests;

/// <summary>
/// Unit tier — no Docker, no network.
/// All broker SDK calls go through hand-rolled fakes that implement the
/// IServiceBusSender / IServiceBusReceiver / IServiceBusClientAdapter seam.
/// </summary>
public class AzureServiceBusTargetTests : IAsyncDisposable
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(0);

    private readonly FakeServiceBusClientAdapter fakeClient = new();
    private readonly AzureServiceBusTarget sut;

    public AzureServiceBusTargetTests()
    {
        sut = new(fakeClient);
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

        FakeServiceBusSender sender = fakeClient.GetSender("orders");
        Assert.Equal("msg-42", sender.LastSentMessage!.MessageId);
    }

    [Fact]
    public async Task ExecuteAsync_SetsBody_FromMessageInteractionBodyBytes()
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"value\":1}");
        MessageInteraction interaction = new("orders", body, EmptyHeaders, "msg-body");

        await sut.ExecuteAsync(interaction, CancellationToken.None);

        FakeServiceBusSender sender = fakeClient.GetSender("orders");
        Assert.Equal(body, sender.LastSentMessage!.Body.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_SetsApplicationProperties_FromHeaders()
    {
        Dictionary<string, string> headers = new()
        {
            ["content-type"] = "application/json",
            ["x-custom"] = "hello",
        };
        MessageInteraction interaction = new("orders", ReadOnlyMemory<byte>.Empty, headers, "msg-headers");

        await sut.ExecuteAsync(interaction, CancellationToken.None);

        FakeServiceBusSender sender = fakeClient.GetSender("orders");
        Assert.Equal("application/json", sender.LastSentMessage!.ApplicationProperties["content-type"]);
        Assert.Equal("hello", sender.LastSentMessage!.ApplicationProperties["x-custom"]);
    }

    [Fact]
    public async Task ExecuteAsync_SetsCorrelationId_WhenPresent()
    {
        MessageInteraction interaction = new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "msg-corr", "corr-99");

        await sut.ExecuteAsync(interaction, CancellationToken.None);

        FakeServiceBusSender sender = fakeClient.GetSender("orders");
        Assert.Equal("corr-99", sender.LastSentMessage!.CorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_NullCorrelationId_LeavesCorrelationIdUnset()
    {
        MessageInteraction interaction = new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "msg-nocorr");

        await sut.ExecuteAsync(interaction, CancellationToken.None);

        FakeServiceBusSender sender = fakeClient.GetSender("orders");
        Assert.Null(sender.LastSentMessage!.CorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_CallsSendMessageAsync_ExactlyOnce()
    {
        MessageInteraction interaction = new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "msg-once");

        await sut.ExecuteAsync(interaction, CancellationToken.None);

        FakeServiceBusSender sender = fakeClient.GetSender("orders");
        Assert.Equal(1, sender.SendCount);
    }

    // -----------------------------------------------------------------------
    // ReceiveAsync — receive translation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReceiveAsync_TranslatesMessageId_FromReceivedMessage()
    {
        FakeServiceBusReceiver receiver = fakeClient.GetReceiver("events");
        receiver.Enqueue("msg-rx", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(0), null);

        MessageInteraction? result = await sut.ReceiveAsync("events", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("msg-rx", result.MessageId);
    }

    [Fact]
    public async Task ReceiveAsync_TranslatesBody_FromReceivedMessage()
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"x\":2}");
        FakeServiceBusReceiver receiver = fakeClient.GetReceiver("events");
        receiver.Enqueue("msg-rxbody", body, new Dictionary<string, string>(0), null);

        MessageInteraction? result = await sut.ReceiveAsync("events", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(body, result.Body.ToArray());
    }

    [Fact]
    public async Task ReceiveAsync_TranslatesApplicationProperties_ToHeaders()
    {
        Dictionary<string, string> props = new() { ["content-type"] = "application/json" };
        FakeServiceBusReceiver receiver = fakeClient.GetReceiver("events");
        receiver.Enqueue("msg-rxprops", ReadOnlyMemory<byte>.Empty, props, null);

        MessageInteraction? result = await sut.ReceiveAsync("events", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("application/json", result.Headers["content-type"]);
    }

    [Fact]
    public async Task ReceiveAsync_TranslatesCorrelationId_FromReceivedMessage()
    {
        FakeServiceBusReceiver receiver = fakeClient.GetReceiver("events");
        receiver.Enqueue("msg-rxcorr", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(0), "corr-rx");

        MessageInteraction? result = await sut.ReceiveAsync("events", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("corr-rx", result.CorrelationId);
    }

    [Fact]
    public async Task ReceiveAsync_ReturnsNull_WhenFakeReceiverReturnsNull()
    {
        // fake receiver has nothing queued — simulates broker timeout returning null
        MessageInteraction? result = await sut.ReceiveAsync("empty-q", TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // AcknowledgeAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AcknowledgeAsync_CallsCompleteMessageAsync_OnReceiver()
    {
        FakeServiceBusReceiver receiver = fakeClient.GetReceiver("ack-q");
        receiver.Enqueue("msg-ack", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(0), null);

        MessageInteraction? received = await sut.ReceiveAsync("ack-q", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(received);
        await sut.AcknowledgeAsync(received, CancellationToken.None);

        Assert.Equal(1, receiver.CompleteCount);
    }

    // -----------------------------------------------------------------------
    // RejectAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RejectAsync_Requeue_True_CallsAbandonMessageAsync()
    {
        FakeServiceBusReceiver receiver = fakeClient.GetReceiver("rej-q");
        receiver.Enqueue("msg-abandon", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(0), null);

        MessageInteraction? received = await sut.ReceiveAsync("rej-q", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(received);
        await sut.RejectAsync(received, requeue: true, CancellationToken.None);

        Assert.Equal(1, receiver.AbandonCount);
        Assert.Equal(0, receiver.DeadLetterCount);
    }

    [Fact]
    public async Task RejectAsync_Requeue_False_CallsDeadLetterMessageAsync()
    {
        FakeServiceBusReceiver receiver = fakeClient.GetReceiver("dl-q");
        receiver.Enqueue("msg-dl", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(0), null);

        MessageInteraction? received = await sut.ReceiveAsync("dl-q", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(received);
        await sut.RejectAsync(received, requeue: false, CancellationToken.None);

        Assert.Equal(0, receiver.AbandonCount);
        Assert.Equal(1, receiver.DeadLetterCount);
    }

    // -----------------------------------------------------------------------
    // Lazy creation and caching
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sender_IsLazilyCreated_OnFirstExecuteAsync()
    {
        Assert.Equal(0, fakeClient.SenderCreationCount("orders"));

        await sut.ExecuteAsync(new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "m1"), CancellationToken.None);

        Assert.Equal(1, fakeClient.SenderCreationCount("orders"));
    }

    [Fact]
    public async Task Sender_IsCached_AcrossMultiplePublishes()
    {
        await sut.ExecuteAsync(new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "m1"), CancellationToken.None);
        await sut.ExecuteAsync(new("orders", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "m2"), CancellationToken.None);

        Assert.Equal(1, fakeClient.SenderCreationCount("orders"));
    }

    [Fact]
    public async Task Receiver_IsLazilyCreated_OnFirstReceiveAsync()
    {
        Assert.Equal(0, fakeClient.ReceiverCreationCount("items"));

        await sut.ReceiveAsync("items", TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.Equal(1, fakeClient.ReceiverCreationCount("items"));
    }

    [Fact]
    public async Task Receiver_IsCached_AcrossMultipleReceiveCalls()
    {
        await sut.ReceiveAsync("items", TimeSpan.FromMilliseconds(10), CancellationToken.None);
        await sut.ReceiveAsync("items", TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.Equal(1, fakeClient.ReceiverCreationCount("items"));
    }

    // -----------------------------------------------------------------------
    // DisposeAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_DisposesAllCachedSenders()
    {
        AzureServiceBusTarget target = new(fakeClient);
        await target.ExecuteAsync(new("dest-a", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "mx"), CancellationToken.None);
        await target.ExecuteAsync(new("dest-b", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "my"), CancellationToken.None);

        await target.DisposeAsync();

        Assert.True(fakeClient.GetSender("dest-a").IsDisposed);
        Assert.True(fakeClient.GetSender("dest-b").IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllCachedReceivers()
    {
        AzureServiceBusTarget target = new(fakeClient);
        await target.ReceiveAsync("rx-a", TimeSpan.FromMilliseconds(10), CancellationToken.None);
        await target.ReceiveAsync("rx-b", TimeSpan.FromMilliseconds(10), CancellationToken.None);

        await target.DisposeAsync();

        Assert.True(fakeClient.GetReceiver("rx-a").IsDisposed);
        Assert.True(fakeClient.GetReceiver("rx-b").IsDisposed);
    }
}
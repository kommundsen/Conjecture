// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Interactions;
using Conjecture.Messaging;

namespace Conjecture.Messaging.Tests;

public class InMemoryMessageBusTargetTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>(0);

    [Fact]
    public async Task PublishThenReceive_ReturnsSameMessage()
    {
        InMemoryMessageBusTarget target = new();
        MessageInteraction sent = new("orders", new byte[] { 1, 2, 3 }, EmptyHeaders, "msg-1");

        await target.ExecuteAsync(sent, CancellationToken.None);
        MessageInteraction? received = await target.ReceiveAsync("orders", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(sent, received);
    }

    [Fact]
    public async Task ReceiveAsync_OnEmptyDestination_ReturnsNullAfterTimeout()
    {
        InMemoryMessageBusTarget target = new();

        MessageInteraction? received = await target.ReceiveAsync("empty-queue", TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.Null(received);
    }

    [Fact]
    public async Task TwoDestinations_AreIsolated()
    {
        InMemoryMessageBusTarget target = new();
        MessageInteraction msgA = new("dest-a", "\n"u8.ToArray(), EmptyHeaders, "msg-a");

        await target.ExecuteAsync(msgA, CancellationToken.None);
        MessageInteraction? receivedFromB = await target.ReceiveAsync("dest-b", TimeSpan.FromMilliseconds(50), CancellationToken.None);
        MessageInteraction? receivedFromA = await target.ReceiveAsync("dest-a", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Null(receivedFromB);
        Assert.NotNull(receivedFromA);
        Assert.Equal(msgA, receivedFromA);
    }

    [Fact]
    public async Task AcknowledgeAsync_AfterReceive_RemovesMessagePermanently()
    {
        InMemoryMessageBusTarget target = new();
        MessageInteraction msg = new("queue", new byte[] { 7 }, EmptyHeaders, "msg-ack");

        await target.ExecuteAsync(msg, CancellationToken.None);
        MessageInteraction? received = await target.ReceiveAsync("queue", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(received);

        await target.AcknowledgeAsync(received, CancellationToken.None);

        MessageInteraction? second = await target.ReceiveAsync("queue", TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.Null(second);
    }

    [Fact]
    public async Task RejectAsync_WithRequeue_MakesMessageAvailableAgain()
    {
        InMemoryMessageBusTarget target = new();
        MessageInteraction msg = new("queue", new byte[] { 5 }, EmptyHeaders, "msg-requeue");

        await target.ExecuteAsync(msg, CancellationToken.None);
        MessageInteraction? first = await target.ReceiveAsync("queue", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(first);

        await target.RejectAsync(first, requeue: true, CancellationToken.None);

        MessageInteraction? second = await target.ReceiveAsync("queue", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(second);
        Assert.Equal(msg, second);
    }

    [Fact]
    public async Task RejectAsync_WithoutRequeue_DiscardsMessage()
    {
        InMemoryMessageBusTarget target = new();
        MessageInteraction msg = new("queue", new byte[] { 3 }, EmptyHeaders, "msg-discard");

        await target.ExecuteAsync(msg, CancellationToken.None);
        MessageInteraction? first = await target.ReceiveAsync("queue", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(first);

        await target.RejectAsync(first, requeue: false, CancellationToken.None);

        MessageInteraction? second = await target.ReceiveAsync("queue", TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.Null(second);
    }

    [Fact]
    public async Task ReceiveAsync_CancellationToken_ThrowsOperationCanceledException()
    {
        InMemoryMessageBusTarget target = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            static async () =>
            {
                InMemoryMessageBusTarget t = new();
                using CancellationTokenSource c = new();
                c.Cancel();
                await t.ReceiveAsync("queue", TimeSpan.FromSeconds(10), c.Token);
            });
    }

    [Fact]
    public async Task Body_ByteContent_SurvivesRoundTrip()
    {
        InMemoryMessageBusTarget target = new();
        byte[] body = Encoding.UTF8.GetBytes("{\"value\":42}");
        MessageInteraction sent = new("data", body, EmptyHeaders, "msg-body");

        await target.ExecuteAsync(sent, CancellationToken.None);
        MessageInteraction? received = await target.ReceiveAsync("data", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(body, received.Body.ToArray());
    }

    [Fact]
    public async Task HeadersMessageIdCorrelationId_SurviveRoundTrip()
    {
        InMemoryMessageBusTarget target = new();
        Dictionary<string, string> headers = new() { ["content-type"] = "application/json", ["x-custom"] = "hello" };
        MessageInteraction sent = new("events", ReadOnlyMemory<byte>.Empty, headers, "msg-meta", "corr-42");

        await target.ExecuteAsync(sent, CancellationToken.None);
        MessageInteraction? received = await target.ReceiveAsync("events", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal("msg-meta", received.MessageId);
        Assert.Equal("corr-42", received.CorrelationId);
        Assert.Equal("application/json", received.Headers["content-type"]);
        Assert.Equal("hello", received.Headers["x-custom"]);
    }

    [Fact]
    public void InMemoryMessageBusTarget_ImplementsIMessageBusTarget()
    {
        InMemoryMessageBusTarget target = new();
        Assert.IsAssignableFrom<IMessageBusTarget>(target);
    }

    [Fact]
    public void InMemoryMessageBusTarget_ImplementsIInteractionTarget()
    {
        InMemoryMessageBusTarget target = new();
        Assert.IsAssignableFrom<IInteractionTarget>(target);
    }
}
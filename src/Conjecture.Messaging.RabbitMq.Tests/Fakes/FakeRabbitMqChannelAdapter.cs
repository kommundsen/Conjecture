// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Messaging.RabbitMq;

namespace Conjecture.Messaging.RabbitMq.Tests;

internal sealed class FakeRabbitMqChannelAdapter : IRabbitMqChannelAdapter
{
    private readonly Queue<FakeRabbitMqReceivedMessageAdapter> receiveQueue = [];

    internal FakePublishedMessage? LastPublished { get; private set; }
    internal int AckCount { get; private set; }
    internal int NackCount { get; private set; }
    internal bool LastNackRequeue { get; private set; }
    internal bool IsDisposed { get; private set; }

    internal void Enqueue(
        ulong deliveryTag,
        string messageId,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, object> headers,
        string? correlationId)
    {
        receiveQueue.Enqueue(new(deliveryTag, messageId, body, headers, correlationId));
    }

    public void BasicPublish(
        string queue,
        string messageId,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, object> headers,
        string? correlationId)
    {
        LastPublished = new(queue, messageId, body, headers, correlationId);
    }

    public IRabbitMqReceivedMessageAdapter? BasicGet(string queue)
    {
        return receiveQueue.Count > 0 ? receiveQueue.Dequeue() : null;
    }

    public void BasicAck(ulong deliveryTag)
    {
        AckCount++;
    }

    public void BasicNack(ulong deliveryTag, bool requeue)
    {
        NackCount++;
        LastNackRequeue = requeue;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
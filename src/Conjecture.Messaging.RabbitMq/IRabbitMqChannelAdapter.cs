// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

namespace Conjecture.Messaging.RabbitMq;

/// <summary>Abstraction over <c>RabbitMQ.Client.IModel</c> for testability.</summary>
public interface IRabbitMqChannelAdapter : IDisposable
{
    /// <summary>Publishes a message to the given queue.</summary>
    void BasicPublish(
        string queue,
        string messageId,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, object> headers,
        string? correlationId);

    /// <summary>Synchronously gets one message from the queue. Returns <see langword="null"/> when the queue is empty.</summary>
    IRabbitMqReceivedMessageAdapter? BasicGet(string queue);

    /// <summary>Acknowledges a delivery.</summary>
    void BasicAck(ulong deliveryTag);

    /// <summary>Negatively acknowledges a delivery, optionally requeueing.</summary>
    void BasicNack(ulong deliveryTag, bool requeue);
}
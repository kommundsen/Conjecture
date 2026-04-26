// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using RabbitMQ.Client;

namespace Conjecture.Messaging.RabbitMq;

/// <summary>Production implementation of <see cref="IRabbitMqChannelAdapter"/> over <see cref="IChannel"/>.</summary>
internal sealed class RabbitMqChannelAdapter(IChannel inner) : IRabbitMqChannelAdapter
{
    /// <inheritdoc/>
    public void BasicPublish(
        string queue,
        string messageId,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, object> headers,
        string? correlationId)
    {
        Dictionary<string, object?> rabbitHeaders = new(headers.Count);
        foreach (KeyValuePair<string, object> kv in headers)
        {
            rabbitHeaders[kv.Key] = kv.Value;
        }

        BasicProperties props = new()
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            Headers = rabbitHeaders,
        };

        inner.BasicPublishAsync(exchange: string.Empty, routingKey: queue, mandatory: false, props, body)
            .GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public IRabbitMqReceivedMessageAdapter? BasicGet(string queue)
    {
        BasicGetResult? result = inner.BasicGetAsync(queue, autoAck: false).GetAwaiter().GetResult();
        return result is null ? null : new RabbitMqReceivedMessageAdapter(result);
    }

    /// <inheritdoc/>
    public void BasicAck(ulong deliveryTag)
    {
        inner.BasicAckAsync(deliveryTag, multiple: false).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public void BasicNack(ulong deliveryTag, bool requeue)
    {
        inner.BasicNackAsync(deliveryTag, multiple: false, requeue).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        inner.Dispose();
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using RabbitMQ.Client;

namespace Conjecture.Messaging.RabbitMq;

/// <summary>Production implementation of <see cref="IRabbitMqReceivedMessageAdapter"/> over <see cref="BasicGetResult"/>.</summary>
internal sealed class RabbitMqReceivedMessageAdapter : IRabbitMqReceivedMessageAdapter
{
    internal RabbitMqReceivedMessageAdapter(BasicGetResult result)
    {
        DeliveryTag = result.DeliveryTag;
        MessageId = result.BasicProperties.MessageId ?? string.Empty;
        Body = result.Body;
        CorrelationId = result.BasicProperties.CorrelationId;

        IDictionary<string, object?>? raw = result.BasicProperties.Headers;
        if (raw is null)
        {
            Headers = new Dictionary<string, object>(0);
        }
        else
        {
            Dictionary<string, object> decoded = new(raw.Count);
            foreach (KeyValuePair<string, object?> kv in raw)
            {
                decoded[kv.Key] = kv.Value switch
                {
                    byte[] bytes => Encoding.UTF8.GetString(bytes),
                    null => string.Empty,
                    _ => Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                };
            }

            Headers = decoded;
        }
    }

    /// <inheritdoc/>
    public ulong DeliveryTag { get; }

    /// <inheritdoc/>
    public string MessageId { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Body { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> Headers { get; }

    /// <inheritdoc/>
    public string? CorrelationId { get; }
}
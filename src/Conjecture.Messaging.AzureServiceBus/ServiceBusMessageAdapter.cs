// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

namespace Conjecture.Messaging.AzureServiceBus;

/// <summary>Wraps an outbound message for sending via <see cref="IServiceBusSender"/>.</summary>
internal sealed class ServiceBusMessageAdapter : IServiceBusMessageAdapter
{
    internal ServiceBusMessageAdapter(
        string messageId,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string> headers,
        string? correlationId)
    {
        MessageId = messageId;
        Body = new(body.ToArray());
        CorrelationId = correlationId;
        ApplicationProperties = new Dictionary<string, object>(headers.Count);

        foreach (KeyValuePair<string, string> header in headers)
        {
            ApplicationProperties[header.Key] = header.Value;
        }
    }

    public string MessageId { get; }

    public BinaryData Body { get; }

    public IDictionary<string, object> ApplicationProperties { get; }

    public string? CorrelationId { get; }
}
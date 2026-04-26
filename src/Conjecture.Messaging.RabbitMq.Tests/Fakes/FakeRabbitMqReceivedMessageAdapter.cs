// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Messaging.RabbitMq;

namespace Conjecture.Messaging.RabbitMq.Tests;

/// <summary>Fake <see cref="IRabbitMqReceivedMessageAdapter"/> used by <see cref="FakeRabbitMqChannelAdapter"/>.</summary>
internal sealed class FakeRabbitMqReceivedMessageAdapter(
    ulong deliveryTag,
    string messageId,
    ReadOnlyMemory<byte> body,
    IReadOnlyDictionary<string, object> headers,
    string? correlationId) : IRabbitMqReceivedMessageAdapter
{
    public ulong DeliveryTag { get; } = deliveryTag;
    public string MessageId { get; } = messageId;
    public ReadOnlyMemory<byte> Body { get; } = body;
    public IReadOnlyDictionary<string, object> Headers { get; } = headers;
    public string? CorrelationId { get; } = correlationId;
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

namespace Conjecture.Messaging.RabbitMq.Tests;

/// <summary>Captures args passed to <see cref="FakeRabbitMqChannelAdapter.BasicPublish"/>.</summary>
internal sealed class FakePublishedMessage(
    string queue,
    string messageId,
    ReadOnlyMemory<byte> body,
    IReadOnlyDictionary<string, object> headers,
    string? correlationId)
{
    internal string Queue { get; } = queue;
    internal string MessageId { get; } = messageId;
    internal ReadOnlyMemory<byte> Body { get; } = body;
    internal IReadOnlyDictionary<string, object> Headers { get; } = headers;
    internal string? CorrelationId { get; } = correlationId;
}
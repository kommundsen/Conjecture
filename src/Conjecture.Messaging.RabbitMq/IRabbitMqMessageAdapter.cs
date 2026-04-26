// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

namespace Conjecture.Messaging.RabbitMq;

/// <summary>Abstraction over a RabbitMQ outbound message for testability.</summary>
public interface IRabbitMqMessageAdapter
{
    /// <summary>Gets the message id.</summary>
    string MessageId { get; }

    /// <summary>Gets the raw message body.</summary>
    ReadOnlyMemory<byte> Body { get; }

    /// <summary>Gets the message headers (values stored as <c>byte[]</c> by RabbitMQ).</summary>
    IReadOnlyDictionary<string, object> Headers { get; }

    /// <summary>Gets the correlation id, or <see langword="null"/> if unset.</summary>
    string? CorrelationId { get; }
}
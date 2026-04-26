// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Messaging.AzureServiceBus;

/// <summary>Abstraction over <c>Azure.Messaging.ServiceBus.ServiceBusMessage</c> for testability.</summary>
public interface IServiceBusMessageAdapter
{
    /// <summary>Gets the message id.</summary>
    string MessageId { get; }

    /// <summary>Gets the message body.</summary>
    BinaryData Body { get; }

    /// <summary>Gets the application properties (headers).</summary>
    IDictionary<string, object> ApplicationProperties { get; }

    /// <summary>Gets the correlation id, or <see langword="null"/> if unset.</summary>
    string? CorrelationId { get; }
}
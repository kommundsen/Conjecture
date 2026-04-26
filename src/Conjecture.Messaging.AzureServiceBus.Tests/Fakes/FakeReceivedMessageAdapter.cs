// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Messaging.AzureServiceBus;

namespace Conjecture.Messaging.AzureServiceBus.Tests;

/// <summary>Fake <see cref="IServiceBusReceivedMessageAdapter"/> used by <see cref="FakeServiceBusReceiver"/>.</summary>
internal sealed class FakeReceivedMessageAdapter : IServiceBusReceivedMessageAdapter
{
    internal FakeReceivedMessageAdapter(
        string messageId,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string> applicationProperties,
        string? correlationId)
    {
        MessageId = messageId;
        Body = new(body.ToArray());
        ApplicationProperties = applicationProperties;
        CorrelationId = correlationId;
    }

    public string MessageId { get; }
    public BinaryData Body { get; }
    public IReadOnlyDictionary<string, string> ApplicationProperties { get; }
    public string? CorrelationId { get; }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

namespace Conjecture.Messaging.AzureServiceBus.Tests;

/// <summary>Fake stand-in for the data emitted by <see cref="IServiceBusSender.SendMessageAsync"/>.</summary>
internal sealed class FakeServiceBusMessage
{
    internal FakeServiceBusMessage(
        string messageId,
        BinaryData body,
        IDictionary<string, object> applicationProperties,
        string? correlationId)
    {
        MessageId = messageId;
        Body = body;
        ApplicationProperties = applicationProperties;
        CorrelationId = correlationId;
    }

    internal string MessageId { get; }
    internal BinaryData Body { get; }
    internal IDictionary<string, object> ApplicationProperties { get; }
    internal string? CorrelationId { get; }
}
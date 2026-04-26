// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus;

namespace Conjecture.Messaging.AzureServiceBus;

/// <summary>Production implementation of <see cref="IServiceBusSender"/> over <see cref="ServiceBusSender"/>.</summary>
internal sealed class ServiceBusSenderAdapter(ServiceBusSender inner) : IServiceBusSender
{
    /// <inheritdoc/>
    public async Task SendMessageAsync(IServiceBusMessageAdapter message, CancellationToken ct)
    {
        ServiceBusMessage sdkMessage = new(message.Body)
        {
            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
        };

        foreach (System.Collections.Generic.KeyValuePair<string, object> prop in message.ApplicationProperties)
        {
            sdkMessage.ApplicationProperties[prop.Key] = prop.Value;
        }

        await inner.SendMessageAsync(sdkMessage, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return inner.DisposeAsync();
    }
}
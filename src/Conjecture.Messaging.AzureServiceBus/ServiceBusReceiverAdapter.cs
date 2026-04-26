// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus;

namespace Conjecture.Messaging.AzureServiceBus;

/// <summary>Production implementation of <see cref="IServiceBusReceiver"/> over <see cref="ServiceBusReceiver"/>.</summary>
internal sealed class ServiceBusReceiverAdapter(ServiceBusReceiver inner) : IServiceBusReceiver
{
    /// <inheritdoc/>
    public async Task<IServiceBusReceivedMessageAdapter?> ReceiveMessageAsync(TimeSpan maxWaitTime, CancellationToken ct)
    {
        ServiceBusReceivedMessage? received = await inner.ReceiveMessageAsync(maxWaitTime, ct).ConfigureAwait(false);
        return received is null ? null : new ReceivedMessageAdapter(received);
    }

    /// <inheritdoc/>
    public async Task CompleteMessageAsync(IServiceBusReceivedMessageAdapter message, CancellationToken ct)
    {
        ReceivedMessageAdapter adapter = (ReceivedMessageAdapter)message;
        await inner.CompleteMessageAsync(adapter.Inner, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AbandonMessageAsync(IServiceBusReceivedMessageAdapter message, CancellationToken ct)
    {
        ReceivedMessageAdapter adapter = (ReceivedMessageAdapter)message;
        await inner.AbandonMessageAsync(adapter.Inner, null, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeadLetterMessageAsync(IServiceBusReceivedMessageAdapter message, CancellationToken ct)
    {
        ReceivedMessageAdapter adapter = (ReceivedMessageAdapter)message;
        await inner.DeadLetterMessageAsync(adapter.Inner, null, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return inner.DisposeAsync();
    }

    private sealed class ReceivedMessageAdapter : IServiceBusReceivedMessageAdapter
    {
        internal ReceivedMessageAdapter(ServiceBusReceivedMessage inner)
        {
            Inner = inner;
            MessageId = inner.MessageId;
            Body = inner.Body;
            CorrelationId = inner.CorrelationId;

            Dictionary<string, string> props = new(inner.ApplicationProperties.Count);
            foreach (KeyValuePair<string, object> prop in inner.ApplicationProperties)
            {
                props[prop.Key] = prop.Value switch
                {
                    string stringValue => stringValue,
                    null => string.Empty,
                    _ => Convert.ToString(prop.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                };
            }

            ApplicationProperties = new ReadOnlyDictionary<string, string>(props);
        }

        internal ServiceBusReceivedMessage Inner { get; }

        public string MessageId { get; }

        public BinaryData Body { get; }

        public IReadOnlyDictionary<string, string> ApplicationProperties { get; }

        public string? CorrelationId { get; }
    }
}
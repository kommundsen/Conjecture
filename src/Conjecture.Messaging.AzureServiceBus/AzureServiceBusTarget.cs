// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus;

using Conjecture.Interactions;
using Conjecture.Messaging;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Messaging.AzureServiceBus;

/// <summary>Azure Service Bus adapter implementing <see cref="IMessageBusTarget"/>.</summary>
/// <remarks>Initializes a new instance of <see cref="AzureServiceBusTarget"/> using the given adapter (for tests).</remarks>
public sealed class AzureServiceBusTarget(IServiceBusClientAdapter client) : IMessageBusTarget, IAsyncDisposable
{
    private readonly IServiceBusClientAdapter client = client;
    private readonly ConcurrentDictionary<string, IServiceBusSender> senders = new();
    private readonly ConcurrentDictionary<string, IServiceBusReceiver> receivers = new();

    /// <summary>
    /// Stores the mapping from MessageId to (receiver, receivedMessage) for ack/reject.
    /// Keyed by MessageId.
    /// </summary>
    private readonly ConcurrentDictionary<string, (IServiceBusReceiver Receiver, IServiceBusReceivedMessageAdapter ReceivedMessage)> inflight = new();

    /// <summary>Creates a production <see cref="AzureServiceBusTarget"/> from a connection string.</summary>
    public static AzureServiceBusTarget Connect(string connectionString)
    {
        return new(new ServiceBusClientAdapter(new ServiceBusClient(connectionString)));
    }

    /// <summary>Publishes <paramref name="message"/> to its destination queue/topic.</summary>
    public async Task<object?> ExecuteAsync(MessageInteraction message, CancellationToken ct)
    {
        IServiceBusSender sender = senders.GetOrAdd(message.Destination, static (dest, c) => c.CreateSender(dest), client);
        ServiceBusMessageAdapter adapter = new(message.MessageId, message.Body, message.Headers, message.CorrelationId);
        await sender.SendMessageAsync(adapter, ct).ConfigureAwait(false);
        return null;
    }

    /// <inheritdoc/>
    Task<object?> IInteractionTarget.ExecuteAsync(IInteraction interaction, CancellationToken ct)
    {
        return interaction is not MessageInteraction message
            ? throw new ArgumentException(
                $"Expected {nameof(MessageInteraction)} but got {interaction?.GetType().Name ?? "null"}.",
                nameof(interaction))
            : ExecuteAsync(message, ct);
    }

    /// <inheritdoc/>
    public async Task<MessageInteraction?> ReceiveAsync(string destination, TimeSpan timeout, CancellationToken ct)
    {
        IServiceBusReceiver receiver = receivers.GetOrAdd(destination, static (dest, c) => c.CreateReceiver(dest), client);
        IServiceBusReceivedMessageAdapter? received = await receiver.ReceiveMessageAsync(timeout, ct).ConfigureAwait(false);

        if (received is null)
        {
            return null;
        }

        Dictionary<string, string> headers = new(received.ApplicationProperties.Count);
        foreach (KeyValuePair<string, string> prop in received.ApplicationProperties)
        {
            headers[prop.Key] = prop.Value;
        }

        MessageInteraction interaction = new(
            destination,
            received.Body.ToMemory(),
            headers,
            received.MessageId,
            received.CorrelationId);

        inflight[received.MessageId] = (receiver, received);
        return interaction;
    }

    /// <inheritdoc/>
    public async Task AcknowledgeAsync(MessageInteraction message, CancellationToken ct)
    {
        if (!inflight.TryRemove(message.MessageId, out (IServiceBusReceiver Receiver, IServiceBusReceivedMessageAdapter ReceivedMessage) token))
        {
            return;
        }

        await token.Receiver.CompleteMessageAsync(token.ReceivedMessage, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RejectAsync(MessageInteraction message, bool requeue, CancellationToken ct)
    {
        if (!inflight.TryRemove(message.MessageId, out (IServiceBusReceiver Receiver, IServiceBusReceivedMessageAdapter ReceivedMessage) token))
        {
            return;
        }

        if (requeue)
        {
            await token.Receiver.AbandonMessageAsync(token.ReceivedMessage, ct).ConfigureAwait(false);
        }
        else
        {
            await token.Receiver.DeadLetterMessageAsync(token.ReceivedMessage, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (KeyValuePair<string, IServiceBusSender> pair in senders)
        {
            await pair.Value.DisposeAsync().ConfigureAwait(false);
        }

        foreach (KeyValuePair<string, IServiceBusReceiver> pair in receivers)
        {
            await pair.Value.DisposeAsync().ConfigureAwait(false);
        }
    }
}
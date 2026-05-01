// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Messaging;

using RabbitMQ.Client;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Messaging.RabbitMq;

/// <summary>RabbitMQ adapter implementing <see cref="IMessageBusTarget"/>.</summary>
/// <remarks>Initializes a new instance of <see cref="RabbitMqTarget"/> using the given adapter (for tests).</remarks>
public sealed class RabbitMqTarget(IRabbitMqConnectionAdapter connection) : IMessageBusTarget, IAsyncDisposable
{
    private readonly IRabbitMqConnectionAdapter connection = connection;
    private readonly ConcurrentDictionary<string, IRabbitMqChannelAdapter> channels = new();
    private readonly ConcurrentDictionary<string, (IRabbitMqChannelAdapter Channel, ulong DeliveryTag)> inflight = new();

    /// <summary>Creates a production <see cref="RabbitMqTarget"/> from a connection string (synchronous).</summary>
    public static RabbitMqTarget Connect(string connectionString)
    {
        ConnectionFactory factory = new() { Uri = new(connectionString) };
        IConnection connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        return new(new RabbitMqConnectionAdapter(connection));
    }

    /// <summary>Creates a production <see cref="RabbitMqTarget"/> from a connection string (asynchronous).</summary>
    public static async Task<RabbitMqTarget> ConnectAsync(string connectionString, CancellationToken ct)
    {
        ConnectionFactory factory = new() { Uri = new(connectionString) };
        IConnection connection = await factory.CreateConnectionAsync(ct).ConfigureAwait(false);
        return new(new RabbitMqConnectionAdapter(connection));
    }

    /// <summary>Publishes <paramref name="message"/> to its destination queue.</summary>
    public Task<object?> ExecuteAsync(MessageInteraction message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IRabbitMqChannelAdapter channel = channels.GetOrAdd(message.Destination, static (dest, conn) => conn.CreateChannel(dest), connection);

        Dictionary<string, object> rabbitHeaders = new(message.Headers.Count);
        foreach (KeyValuePair<string, string> kv in message.Headers)
        {
            rabbitHeaders[kv.Key] = Encoding.UTF8.GetBytes(kv.Value);
        }

        channel.BasicPublish(message.Destination, message.MessageId, message.Body, rabbitHeaders, message.CorrelationId);
        return Task.FromResult<object?>(null);
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
    public Task<MessageInteraction?> ReceiveAsync(string destination, TimeSpan timeout, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IRabbitMqChannelAdapter channel = channels.GetOrAdd(destination, static (dest, conn) => conn.CreateChannel(dest), connection);
        IRabbitMqReceivedMessageAdapter? received = channel.BasicGet(destination);

        if (received is null)
        {
            return Task.FromResult<MessageInteraction?>(null);
        }

        Dictionary<string, string> headers = new(received.Headers.Count);
        foreach (KeyValuePair<string, object> kv in received.Headers)
        {
            string value = kv.Value is byte[] bytes
                ? Encoding.UTF8.GetString(bytes)
                : Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            headers[kv.Key] = value;
        }

        MessageInteraction interaction = new(
            destination,
            received.Body,
            headers,
            received.MessageId,
            received.CorrelationId);

        inflight[received.MessageId] = (channel, received.DeliveryTag);
        return Task.FromResult<MessageInteraction?>(interaction);
    }

    /// <inheritdoc/>
    public Task AcknowledgeAsync(MessageInteraction message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!inflight.TryRemove(message.MessageId, out (IRabbitMqChannelAdapter Channel, ulong DeliveryTag) token))
        {
            return Task.CompletedTask;
        }

        token.Channel.BasicAck(token.DeliveryTag);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RejectAsync(MessageInteraction message, bool requeue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!inflight.TryRemove(message.MessageId, out (IRabbitMqChannelAdapter Channel, ulong DeliveryTag) token))
        {
            return Task.CompletedTask;
        }

        token.Channel.BasicNack(token.DeliveryTag, requeue);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        foreach (KeyValuePair<string, IRabbitMqChannelAdapter> pair in channels)
        {
            pair.Value.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
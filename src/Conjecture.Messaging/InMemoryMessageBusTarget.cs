// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Conjecture.Interactions;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Messaging;

/// <summary>In-memory reference adapter for <see cref="IMessageBusTarget"/>.</summary>
public sealed class InMemoryMessageBusTarget : IMessageBusTarget
{
    private readonly ConcurrentDictionary<string, Channel<MessageInteraction>> channels = new();

    private Channel<MessageInteraction> GetOrCreateChannel(string destination)
    {
        return channels.GetOrAdd(destination, static _ => Channel.CreateUnbounded<MessageInteraction>());
    }

    /// <inheritdoc/>
    public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
    {
        if (interaction is not MessageInteraction message)
        {
            throw new ArgumentException(
                $"Expected {nameof(MessageInteraction)} but got {interaction?.GetType().Name ?? "null"}.",
                nameof(interaction));
        }

        Channel<MessageInteraction> channel = GetOrCreateChannel(message.Destination);
        channel.Writer.TryWrite(message);
        return Task.FromResult<object?>(null);
    }

    /// <inheritdoc/>
    public async Task<MessageInteraction?> ReceiveAsync(string destination, TimeSpan timeout, CancellationToken ct)
    {
        Channel<MessageInteraction> channel = GetOrCreateChannel(destination);
        using CancellationTokenSource timeoutCts = new(timeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            return await channel.Reader.ReadAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public Task AcknowledgeAsync(MessageInteraction message, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RejectAsync(MessageInteraction message, bool requeue, CancellationToken ct)
    {
        if (requeue)
        {
            Channel<MessageInteraction> channel = GetOrCreateChannel(message.Destination);
            channel.Writer.TryWrite(message);
        }

        return Task.CompletedTask;
    }
}
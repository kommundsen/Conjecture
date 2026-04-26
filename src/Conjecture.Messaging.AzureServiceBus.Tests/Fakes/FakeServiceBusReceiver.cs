// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Messaging.AzureServiceBus;

namespace Conjecture.Messaging.AzureServiceBus.Tests;

internal sealed class FakeServiceBusReceiver : IServiceBusReceiver
{
    private readonly Queue<FakeReceivedMessageAdapter> queue = [];

    internal int CompleteCount { get; private set; }
    internal int AbandonCount { get; private set; }
    internal int DeadLetterCount { get; private set; }
    internal bool IsDisposed { get; private set; }

    internal void Enqueue(
        string messageId,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string> applicationProperties,
        string? correlationId)
    {
        queue.Enqueue(new(messageId, body, applicationProperties, correlationId));
    }

    public Task<IServiceBusReceivedMessageAdapter?> ReceiveMessageAsync(TimeSpan maxWaitTime, CancellationToken ct)
    {
        IServiceBusReceivedMessageAdapter? result = queue.Count > 0 ? queue.Dequeue() : null;
        return Task.FromResult(result);
    }

    public Task CompleteMessageAsync(IServiceBusReceivedMessageAdapter message, CancellationToken ct)
    {
        CompleteCount++;
        return Task.CompletedTask;
    }

    public Task AbandonMessageAsync(IServiceBusReceivedMessageAdapter message, CancellationToken ct)
    {
        AbandonCount++;
        return Task.CompletedTask;
    }

    public Task DeadLetterMessageAsync(IServiceBusReceivedMessageAdapter message, CancellationToken ct)
    {
        DeadLetterCount++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
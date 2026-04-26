// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Conjecture.Messaging.AzureServiceBus;

/// <summary>Abstraction over <c>Azure.Messaging.ServiceBus.ServiceBusReceiver</c> for testability.</summary>
public interface IServiceBusReceiver : IAsyncDisposable
{
    /// <summary>Waits up to <paramref name="maxWaitTime"/> for a message, returning <see langword="null"/> on timeout.</summary>
    Task<IServiceBusReceivedMessageAdapter?> ReceiveMessageAsync(TimeSpan maxWaitTime, CancellationToken ct);

    /// <summary>Completes (acknowledges) the message.</summary>
    Task CompleteMessageAsync(IServiceBusReceivedMessageAdapter message, CancellationToken ct);

    /// <summary>Abandons the message, making it available for redelivery.</summary>
    Task AbandonMessageAsync(IServiceBusReceivedMessageAdapter message, CancellationToken ct);

    /// <summary>Dead-letters the message.</summary>
    Task DeadLetterMessageAsync(IServiceBusReceivedMessageAdapter message, CancellationToken ct);
}
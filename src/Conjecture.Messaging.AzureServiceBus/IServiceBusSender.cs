// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Conjecture.Messaging.AzureServiceBus;

/// <summary>Abstraction over <c>Azure.Messaging.ServiceBus.ServiceBusSender</c> for testability.</summary>
public interface IServiceBusSender : IAsyncDisposable
{
    /// <summary>Sends a single message to the destination.</summary>
    Task SendMessageAsync(IServiceBusMessageAdapter message, CancellationToken ct);
}
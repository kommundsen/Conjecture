// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Threading;
using System.Threading.Tasks;

using Conjecture.Messaging.AzureServiceBus;

namespace Conjecture.Messaging.AzureServiceBus.Tests;

internal sealed class FakeServiceBusSender : IServiceBusSender
{
    internal FakeServiceBusMessage? LastSentMessage { get; private set; }
    internal int SendCount { get; private set; }
    internal bool IsDisposed { get; private set; }

    public Task SendMessageAsync(IServiceBusMessageAdapter message, CancellationToken ct)
    {
        LastSentMessage = new(
            message.MessageId,
            message.Body,
            message.ApplicationProperties,
            message.CorrelationId);
        SendCount++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
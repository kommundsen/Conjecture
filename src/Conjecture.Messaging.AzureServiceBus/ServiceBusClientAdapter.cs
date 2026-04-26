// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Azure.Messaging.ServiceBus;

namespace Conjecture.Messaging.AzureServiceBus;

/// <summary>Production implementation of <see cref="IServiceBusClientAdapter"/> over <see cref="ServiceBusClient"/>.</summary>
internal sealed class ServiceBusClientAdapter(ServiceBusClient inner) : IServiceBusClientAdapter
{
    /// <inheritdoc/>
    public IServiceBusSender CreateSender(string destination)
    {
        return new ServiceBusSenderAdapter(inner.CreateSender(destination));
    }

    /// <inheritdoc/>
    public IServiceBusReceiver CreateReceiver(string destination)
    {
        return new ServiceBusReceiverAdapter(inner.CreateReceiver(destination));
    }
}
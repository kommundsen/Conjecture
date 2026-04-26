// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Messaging.AzureServiceBus;

/// <summary>Factory abstraction over <c>Azure.Messaging.ServiceBus.ServiceBusClient</c> for testability.</summary>
public interface IServiceBusClientAdapter
{
    /// <summary>Creates (or returns a cached) sender for <paramref name="destination"/>.</summary>
    IServiceBusSender CreateSender(string destination);

    /// <summary>Creates (or returns a cached) receiver for <paramref name="destination"/>.</summary>
    IServiceBusReceiver CreateReceiver(string destination);
}
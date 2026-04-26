// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Messaging.AzureServiceBus;

namespace Conjecture.Messaging.AzureServiceBus.Tests;

internal sealed class FakeServiceBusClientAdapter : IServiceBusClientAdapter
{
    private readonly Dictionary<string, FakeServiceBusSender> senders = [];
    private readonly Dictionary<string, FakeServiceBusReceiver> receivers = [];
    private readonly Dictionary<string, int> senderCreations = [];
    private readonly Dictionary<string, int> receiverCreations = [];

    public IServiceBusSender CreateSender(string destination)
    {
        if (!senders.TryGetValue(destination, out FakeServiceBusSender? sender))
        {
            sender = new();
            senders[destination] = sender;
        }

        senderCreations[destination] = senderCreations.GetValueOrDefault(destination) + 1;
        return sender;
    }

    public IServiceBusReceiver CreateReceiver(string destination)
    {
        if (!receivers.TryGetValue(destination, out FakeServiceBusReceiver? receiver))
        {
            receiver = new();
            receivers[destination] = receiver;
        }

        receiverCreations[destination] = receiverCreations.GetValueOrDefault(destination) + 1;
        return receiver;
    }

    internal FakeServiceBusSender GetSender(string destination)
    {
        return senders[destination];
    }

    internal FakeServiceBusReceiver GetReceiver(string destination)
    {
        if (!receivers.TryGetValue(destination, out FakeServiceBusReceiver? receiver))
        {
            receiver = new();
            receivers[destination] = receiver;
        }

        return receiver;
    }

    internal int SenderCreationCount(string destination)
    {
        return senderCreations.GetValueOrDefault(destination);
    }

    internal int ReceiverCreationCount(string destination)
    {
        return receiverCreations.GetValueOrDefault(destination);
    }
}
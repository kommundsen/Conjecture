// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Messaging.RabbitMq;

namespace Conjecture.Messaging.RabbitMq.Tests;

internal sealed class FakeRabbitMqConnectionAdapter : IRabbitMqConnectionAdapter
{
    private readonly Dictionary<string, FakeRabbitMqChannelAdapter> channels = [];
    private readonly Dictionary<string, int> channelCreations = [];

    public IRabbitMqChannelAdapter CreateChannel(string queue)
    {
        if (!channels.TryGetValue(queue, out FakeRabbitMqChannelAdapter? channel))
        {
            channel = new();
            channels[queue] = channel;
        }

        channelCreations[queue] = channelCreations.GetValueOrDefault(queue) + 1;
        return channel;
    }

    internal FakeRabbitMqChannelAdapter GetChannel(string queue)
    {
        if (!channels.TryGetValue(queue, out FakeRabbitMqChannelAdapter? channel))
        {
            channel = new();
            channels[queue] = channel;
        }

        return channel;
    }

    internal int ChannelCreationCount(string queue)
    {
        return channelCreations.GetValueOrDefault(queue);
    }
}
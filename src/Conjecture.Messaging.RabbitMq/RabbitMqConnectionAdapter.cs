// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using RabbitMQ.Client;

namespace Conjecture.Messaging.RabbitMq;

/// <summary>Production implementation of <see cref="IRabbitMqConnectionAdapter"/> over <see cref="IConnection"/>.</summary>
internal sealed class RabbitMqConnectionAdapter(IConnection inner) : IRabbitMqConnectionAdapter
{
    /// <inheritdoc/>
    public IRabbitMqChannelAdapter CreateChannel(string queue)
    {
        IChannel channel = inner.CreateChannelAsync().GetAwaiter().GetResult();
        return new RabbitMqChannelAdapter(channel);
    }
}
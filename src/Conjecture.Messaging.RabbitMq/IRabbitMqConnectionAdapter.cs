// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Messaging.RabbitMq;

/// <summary>Factory abstraction over <c>RabbitMQ.Client.IConnection</c> for testability.</summary>
public interface IRabbitMqConnectionAdapter
{
    /// <summary>Creates (or returns a cached) channel for <paramref name="queue"/>.</summary>
    IRabbitMqChannelAdapter CreateChannel(string queue);
}
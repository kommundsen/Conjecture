// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Interactions;

namespace Conjecture.Messaging;

/// <summary>Pull-based message bus target for publishing and consuming messages.</summary>
public interface IMessageBusTarget : IInteractionTarget
{
    /// <summary>Waits up to <paramref name="timeout"/> for a message on <paramref name="destination"/>.</summary>
    Task<MessageInteraction?> ReceiveAsync(string destination, TimeSpan timeout, CancellationToken ct);

    /// <summary>Acknowledges successful processing of <paramref name="message"/>.</summary>
    Task AcknowledgeAsync(MessageInteraction message, CancellationToken ct);

    /// <summary>Rejects <paramref name="message"/>, optionally requeuing it.</summary>
    Task RejectAsync(MessageInteraction message, bool requeue, CancellationToken ct);
}
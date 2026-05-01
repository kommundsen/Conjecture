// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Interactions;
using Conjecture.Messaging;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Messaging.Tests;

public class IMessageBusTargetTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>(0);

    private sealed class NullBusTarget : IMessageBusTarget
    {
        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            return Task.FromResult<object?>(null);
        }

        public Task<MessageInteraction?> ReceiveAsync(string destination, TimeSpan timeout, CancellationToken ct)
        {
            return Task.FromResult<MessageInteraction?>(null);
        }

        public Task AcknowledgeAsync(MessageInteraction message, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task RejectAsync(MessageInteraction message, bool requeue, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void IMessageBusTarget_ExtendsIInteractionTarget()
    {
        NullBusTarget target = new();
        Assert.IsAssignableFrom<IInteractionTarget>(target);
    }

    [Fact]
    public void IMessageBusTarget_ExtendsIMessageBusTarget()
    {
        NullBusTarget target = new();
        Assert.IsAssignableFrom<IMessageBusTarget>(target);
    }

    [Fact]
    public async Task ReceiveAsync_WhenNoMessage_ReturnsNull()
    {
        NullBusTarget target = new();

        MessageInteraction? result = await target.ReceiveAsync("queue", TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AcknowledgeAsync_DoesNotThrow()
    {
        NullBusTarget target = new();
        MessageInteraction msg = new("q", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "id");

        await target.AcknowledgeAsync(msg, CancellationToken.None);
    }

    [Fact]
    public async Task RejectAsync_WithRequeue_DoesNotThrow()
    {
        NullBusTarget target = new();
        MessageInteraction msg = new("q", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "id");

        await target.RejectAsync(msg, requeue: true, CancellationToken.None);
    }

    [Fact]
    public async Task RejectAsync_WithoutRequeue_DoesNotThrow()
    {
        NullBusTarget target = new();
        MessageInteraction msg = new("q", ReadOnlyMemory<byte>.Empty, EmptyHeaders, "id");

        await target.RejectAsync(msg, requeue: false, CancellationToken.None);
    }
}
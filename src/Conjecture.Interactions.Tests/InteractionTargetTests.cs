// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Threading;
using System.Threading.Tasks;
using Conjecture.Interactions;

namespace Conjecture.Interactions.Tests;

public class InteractionTargetTests
{
    private sealed class TestInteraction : IInteraction { }

    private sealed class NullReturningTarget : IInteractionTarget
    {
        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            return Task.FromResult<object?>(null);
        }
    }

    private sealed class ValueReturningTarget : IInteractionTarget
    {
        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            return Task.FromResult<object?>("hello");
        }
    }

    private sealed class CancellationCheckingTarget : IInteractionTarget
    {
        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<object?>(null);
        }
    }

    [Fact]
    public void IInteraction_ConcreteImplementation_IsAssignableToInterface()
    {
        TestInteraction concrete = new();
        Assert.IsAssignableFrom<IInteraction>(concrete);
    }

    [Fact]
    public async Task ExecuteAsync_NullReturningTarget_ReturnsNull()
    {
        NullReturningTarget target = new();
        TestInteraction interaction = new();
        object? result = await target.ExecuteAsync(interaction, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_ValueReturningTarget_ReturnsExpectedValue()
    {
        ValueReturningTarget target = new();
        TestInteraction interaction = new();
        object? result = await target.ExecuteAsync(interaction, CancellationToken.None);
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        CancellationCheckingTarget target = new();
        TestInteraction interaction = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
            {
                await target.ExecuteAsync(interaction, cts.Token);
            });
    }
}

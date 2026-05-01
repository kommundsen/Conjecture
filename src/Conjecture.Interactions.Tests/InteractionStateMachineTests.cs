// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Core;
using Conjecture.Interactions;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Interactions.Tests;

public class InteractionStateMachineTests
{
    // ─── Test interactions ────────────────────────────────────────────────────

    private sealed class PingInteraction : IInteraction { }
    private sealed class PongInteraction : IInteraction { }

    // ─── Fake targets ─────────────────────────────────────────────────────────

    private sealed class TrackingTarget : IInteractionTarget
    {
        public List<IInteraction> Received { get; } = [];

        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Received.Add(interaction);
            return Task.FromResult<object?>(null);
        }
    }

    private sealed class AlwaysCancellingTarget : IInteractionTarget
    {
        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<object?>(null);
        }
    }

    // ─── Concrete machine ─────────────────────────────────────────────────────

    // Simple counter state: records how many interactions have been dispatched.
    private sealed class CountingMachine : InteractionStateMachine<int>
    {
        public override int InitialState() => 0;

        public override IEnumerable<Strategy<IInteraction>> Commands(int state)
        {
            yield return Strategy.Just<IInteraction>(new PingInteraction());
            yield return Strategy.Just<IInteraction>(new PongInteraction());
        }

        public override async ValueTask<int> RunCommand(
            int state,
            IInteraction interaction,
            IInteractionTarget target,
            CancellationToken ct)
        {
            await target.ExecuteAsync(interaction, ct);
            return state + 1;
        }

        public override void Invariant(int state) { }
    }

    // ─── Sequence generation tests ────────────────────────────────────────────

    [Fact]
    public void Commands_ReturnsTwoStrategies()
    {
        CountingMachine machine = new();
        IEnumerable<Strategy<IInteraction>> commands = machine.Commands(0);
        Assert.Equal(2, commands.Count());
    }

    [Fact]
    public void InitialState_ReturnsZero()
    {
        CountingMachine machine = new();
        Assert.Equal(0, machine.InitialState());
    }

    // ─── Cancellation tests ───────────────────────────────────────────────────

    [Fact]
    public async Task RunCommand_WithCancelledToken_ThrowsOperationCanceledException()
    {
        CountingMachine machine = new();
        AlwaysCancellingTarget target = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await machine.RunCommand(0, new PingInteraction(), target, cts.Token));
    }

    [Fact]
    public async Task RunCommand_WithLiveToken_DispatchesToTarget()
    {
        CountingMachine machine = new();
        TrackingTarget target = new();
        PingInteraction interaction = new();

        await machine.RunCommand(0, interaction, target, CancellationToken.None);

        IInteraction received = Assert.Single(target.Received);
        Assert.Same(interaction, received);
    }

    [Fact]
    public async Task RunCommand_WithLiveToken_ReturnsIncrementedState()
    {
        CountingMachine machine = new();
        TrackingTarget target = new();

        int result = await machine.RunCommand(5, new PingInteraction(), target, CancellationToken.None);

        Assert.Equal(6, result);
    }
}
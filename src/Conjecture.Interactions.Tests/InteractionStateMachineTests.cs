// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conjecture.Core;
using Conjecture.Interactions;

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
            yield return Generate.Just<IInteraction>(new PingInteraction());
            yield return Generate.Just<IInteraction>(new PongInteraction());
        }

        public override int RunCommand(
            int state,
            IInteraction interaction,
            IInteractionTarget target,
            CancellationToken ct)
        {
            target.ExecuteAsync(interaction, ct).GetAwaiter().GetResult();
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

    [Fact]
    public void Generate_StateMachine_ProducesRun()
    {
        Strategy<StateMachineRun<int>> strategy =
            Generate.StateMachine<CountingMachine, int, IInteraction>(maxSteps: 5);
        StateMachineRun<int> run = DataGen.SampleOne(strategy, seed: 42UL);
        Assert.NotNull(run);
    }

    [Fact]
    public void Generate_StateMachine_RunPasses()
    {
        Strategy<StateMachineRun<int>> strategy =
            Generate.StateMachine<CountingMachine, int, IInteraction>(maxSteps: 5);
        StateMachineRun<int> run = DataGen.SampleOne(strategy, seed: 42UL);
        Assert.True(run.Passed);
    }

    [Fact]
    public void Generate_StateMachine_StepCountMatchesFinalState()
    {
        Strategy<StateMachineRun<int>> strategy =
            Generate.StateMachine<CountingMachine, int, IInteraction>(maxSteps: 10);
        StateMachineRun<int> run = DataGen.SampleOne(strategy, seed: 1UL);
        Assert.Equal(run.Steps.Count, run.FinalState);
    }

    // ─── Shrink stability tests ───────────────────────────────────────────────

    // Verifying shrink stability is observable via the public API: generate many
    // samples and confirm the machine can produce runs of varying lengths, which
    // requires that CommandStart sentinels are written (used by CommandSequenceShrinkPass).
    [Fact]
    public void Generate_StateMachine_ProducesVariableLengthRuns()
    {
        Strategy<StateMachineRun<int>> strategy =
            Generate.StateMachine<CountingMachine, int, IInteraction>(maxSteps: 10);
        IReadOnlyList<StateMachineRun<int>> runs = DataGen.Sample(strategy, count: 20, seed: 7UL);
        int minSteps = runs.Min(r => r.Steps.Count);
        int maxSteps = runs.Max(r => r.Steps.Count);
        // Variable-length sequences confirm CommandStart sentinels are present for shrinking
        Assert.True(maxSteps > minSteps,
            $"Expected variable-length runs but got uniform {minSteps} steps across all samples.");
    }

    [Fact]
    public void Generate_StateMachine_MaxStepsZero_ReturnsEmptyRun()
    {
        Strategy<StateMachineRun<int>> strategy =
            Generate.StateMachine<CountingMachine, int, IInteraction>(maxSteps: 0);
        StateMachineRun<int> run = DataGen.SampleOne(strategy, seed: 1UL);
        Assert.Empty(run.Steps);
    }

    // ─── Cancellation tests ───────────────────────────────────────────────────

    [Fact]
    public void RunCommand_WithCancelledToken_ThrowsOperationCanceledException()
    {
        CountingMachine machine = new();
        AlwaysCancellingTarget target = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => machine.RunCommand(0, new PingInteraction(), target, cts.Token));
    }

    [Fact]
    public async Task RunCommand_WithCancelledToken_Async_ThrowsOperationCanceledException()
    {
        CountingMachine machine = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        TrackingTarget target = new();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Task.Run(
                () => machine.RunCommand(0, new PingInteraction(), target, cts.Token),
                CancellationToken.None));
    }

    [Fact]
    public void RunCommand_WithLiveToken_DispatchesToTarget()
    {
        CountingMachine machine = new();
        TrackingTarget target = new();
        PingInteraction interaction = new();

        machine.RunCommand(0, interaction, target, CancellationToken.None);

        IInteraction received = Assert.Single(target.Received);
        Assert.Same(interaction, received);
    }

    [Fact]
    public void RunCommand_WithLiveToken_ReturnsIncrementedState()
    {
        CountingMachine machine = new();
        TrackingTarget target = new();

        int result = machine.RunCommand(5, new PingInteraction(), target, CancellationToken.None);

        Assert.Equal(6, result);
    }
}

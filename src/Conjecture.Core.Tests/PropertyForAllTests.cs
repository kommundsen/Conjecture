// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Core;
using Conjecture.Interactions;

namespace Conjecture.Core.Tests;

public sealed class PropertyForAllTests
{
    private sealed class NoOpTarget : IInteractionTarget
    {
        internal static readonly NoOpTarget Instance = new();

        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct) =>
            Task.FromResult<object?>(null);
    }

    // ── Strategy overload ──────────────────────────────────────────────────

    [Fact]
    public async Task StrategyOverload_PassingAssertion_CompletesWithoutThrowing()
    {
        Strategy<int> strategy = Strategy.Integers<int>(0, 10);
        ConjectureSettings settings = new() { MaxExamples = 20, Seed = 1UL };

        await Property.ForAll(
            NoOpTarget.Instance,
            strategy,
            static (IInteractionTarget _, int value) => value < 0 ? throw new InvalidOperationException("impossible") : Task.CompletedTask,
            settings);
    }

    [Fact]
    public async Task StrategyOverload_FailingAssertion_ThrowsConjectureException()
    {
        Strategy<int> strategy = Strategy.Integers<int>(0, 100);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };

        await Assert.ThrowsAsync<ConjectureException>(async () =>
            await Property.ForAll(
                NoOpTarget.Instance,
                strategy,
                static (IInteractionTarget _, int value) => value > 5 ? throw new InvalidOperationException("too large") : Task.CompletedTask,
                settings));
    }

    [Fact]
    public async Task StrategyOverload_FailingAssertion_ShrinksToBoundary()
    {
        Strategy<int> strategy = Strategy.Integers<int>(0, 100);
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };

        ConjectureException ex = await Assert.ThrowsAsync<ConjectureException>(async () =>
            await Property.ForAll(
                NoOpTarget.Instance,
                strategy,
                static (IInteractionTarget _, int value) => value > 5 ? throw new InvalidOperationException("too large") : Task.CompletedTask,
                settings));

        // The shrunk counterexample should appear in the message
        Assert.Contains("6", ex.Message);
    }

    [Fact]
    public async Task StrategyOverload_CancelledToken_DoesNotRunManyExamples()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Strategy<int> strategy = Strategy.Integers<int>(0, 100);
        int executionCount = 0;

        try
        {
            await Property.ForAll(
                NoOpTarget.Instance,
                strategy,
                (IInteractionTarget _, int _) =>
                {
                    Interlocked.Increment(ref executionCount);
                    return Task.CompletedTask;
                },
                ct: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected: cancellation propagates as OperationCanceledException
        }

        Assert.True(executionCount < 10, $"Should not run many examples with cancelled token, ran {executionCount}");
    }

    [Fact]
    public async Task StrategyOverload_SettingsOverride_UsesProvidedSettings()
    {
        Strategy<int> strategy = Strategy.Integers<int>(0, 10);
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        int executionCount = 0;

        await Property.ForAll(
            NoOpTarget.Instance,
            strategy,
            (IInteractionTarget _, int _) =>
            {
                Interlocked.Increment(ref executionCount);
                return Task.CompletedTask;
            },
            settings);

        Assert.Equal(1, executionCount);
    }

    // ── State machine overload ─────────────────────────────────────────────

    private sealed class PassingCounterMachine : InteractionStateMachine<int>
    {
        private sealed class IncrementInteraction : IInteraction { }

        public override int InitialState() => 0;

        public override IEnumerable<Strategy<IInteraction>> Commands(int state)
        {
            yield return Strategy.Just<IInteraction>(new IncrementInteraction());
        }

        public override int RunCommand(int state, IInteraction interaction, IInteractionTarget target, CancellationToken ct) =>
            state + 1;

        public override void Invariant(int state)
        {
            if (state < 0)
            {
                throw new InvalidOperationException("State must be non-negative.");
            }
        }
    }

    private sealed class FailingCounterMachine : InteractionStateMachine<int>
    {
        private sealed class IncrementInteraction : IInteraction { }

        public override int InitialState() => 0;

        public override IEnumerable<Strategy<IInteraction>> Commands(int state)
        {
            yield return Strategy.Just<IInteraction>(new IncrementInteraction());
        }

        public override int RunCommand(int state, IInteraction interaction, IInteractionTarget target, CancellationToken ct) =>
            state + 1;

        public override void Invariant(int state)
        {
            if (state > 0)
            {
                throw new InvalidOperationException("Invariant violated: state exceeded zero.");
            }
        }
    }

    [Fact]
    public async Task StateMachineOverload_PassingInvariant_CompletesWithoutThrowing()
    {
        PassingCounterMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 20, Seed = 1UL };

        await Property.ForAll(NoOpTarget.Instance, machine, settings);
    }

    [Fact]
    public async Task StateMachineOverload_FailingInvariant_ThrowsConjectureException()
    {
        FailingCounterMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 50, Seed = 1UL };

        await Assert.ThrowsAsync<ConjectureException>(async () =>
            await Property.ForAll(NoOpTarget.Instance, machine, settings));
    }
}
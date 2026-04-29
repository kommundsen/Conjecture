// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.TestingPlatform;

using Xunit;

using ShrinkEngine = Conjecture.Core.Internal.Shrinker;

namespace Conjecture.SelfTests;

/// <summary>
/// Self-tests verifying shrinker invariants when the test subject is a stateful
/// machine with a planted invariant-violation bug.
/// </summary>
public class StateMachineSelfTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static int CountCommandStarts(IReadOnlyList<IRNode> nodes) =>
        nodes.Count(n => n.Kind == IRNodeKind.CommandStart);

    // ─── Commands ─────────────────────────────────────────────────────────────

    private abstract class CounterCommand
    {
        public sealed class Inc : CounterCommand
        {
            public override string ToString() => "Inc()";
        }

        public sealed class Nop : CounterCommand
        {
            public override string ToString() => "Nop()";
        }
    }

    // ─── Machine ──────────────────────────────────────────────────────────────

    // Planted bug: invariant fires when the counter reaches 3.
    // Inc increments; Nop is a no-op included to give the shrinker a varied
    // command alphabet to work through.  Minimal failing sequence: [Inc, Inc, Inc].
    private sealed class BuggyCounterMachine : IStateMachine<int, CounterCommand>
    {
        public int InitialState() => 0;

        public IEnumerable<Strategy<CounterCommand>> Commands(int state)
        {
            yield return Strategy.Just((CounterCommand)new CounterCommand.Inc());
            yield return Strategy.Just((CounterCommand)new CounterCommand.Nop());
        }

        public int RunCommand(int state, CounterCommand cmd) =>
            cmd is CounterCommand.Inc ? state + 1 : state;

        public void Invariant(int state)
        {
            if (state >= 3)
            {
                throw new InvalidOperationException($"Counter exceeded threshold: {state}");
            }
        }
    }

    // ─── Strategy ─────────────────────────────────────────────────────────────

    // Gen.Compose first draws a random step budget in [5, 20], then generates a
    // machine run within that budget, so test inputs vary in length.
    private static Strategy<StateMachineRun<int>> MachineStrategy() =>
        Strategy.Compose(ctx =>
        {
            int maxSteps = ctx.Generate(Strategy.Integers(5, 20));
            return ctx.Generate(Strategy.StateMachine<BuggyCounterMachine, int, CounterCommand>(maxSteps));
        });

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Property]
    public async Task MonotoneShrinking_CommandSequence_ShrunkStepCountLeqOriginal()
    {
        ConjectureSettings settings = new() { Seed = 1UL, MaxExamples = 100, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = MachineStrategy().Generate(data));

        Assert.False(result.Passed);
        int originalSteps = CountCommandStarts(result.OriginalCounterexample!);
        int shrunkSteps = CountCommandStarts(result.Counterexample!);
        Assert.True(shrunkSteps <= originalSteps,
            $"Shrinking increased step count from {originalSteps} to {shrunkSteps}");
    }

    [Property]
    public async Task ShrinkingPreservesFailure_ShrunkCounterexample_ReplaysAsInteresting()
    {
        ConjectureSettings settings = new() { Seed = 1UL, MaxExamples = 100, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = MachineStrategy().Generate(data));

        Assert.False(result.Passed);
        Status status = SelfTestHelpers.Replay(result.Counterexample!,
            data => _ = MachineStrategy().Generate(data));
        Assert.Equal(Status.Interesting, status);
    }

    [Property]
    public async Task CommandSequenceShrinkPass_Idempotent_NoFurtherProgressAfterShrink()
    {
        ConjectureSettings settings = new() { Seed = 1UL, MaxExamples = 100, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = MachineStrategy().Generate(data));

        Assert.False(result.Passed);

        static void Property(ConjectureData data)
        {
            _ = MachineStrategy().Generate(data);
        }
        (IReadOnlyList<IRNode> _, int additionalShrinks) = await ShrinkEngine.ShrinkAsync(
            result.Counterexample!,
            nodes => new ValueTask<Status>(SelfTestHelpers.Replay(nodes, Property)));

        Assert.Equal(0, additionalShrinks);
    }
}
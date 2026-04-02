// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.StateMachine;

// Invariant fails when state reaches 3 — minimal failing sequence is exactly 3 steps.
internal sealed class FailsAtThreeMachine : IStateMachine<int, string>
{
    public int InitialState() => 0;
    public IEnumerable<Strategy<string>> Commands(int state) => [Generate.Just("step")];
    public int RunCommand(int state, string _) => state + 1;
    public void Invariant(int state)
    {
        if (state >= 3)
        {
            throw new InvalidOperationException($"state {state} exceeds limit");
        }
    }
}

public class ShrinkIntegrationTests
{
    private static int CountCommandStarts(IReadOnlyList<IRNode> nodes) =>
        nodes.Count(n => n.Kind == IRNodeKind.CommandStart);

    [Fact]
    public async Task Shrink_StateMachineThatFailsAtThreeSteps_ShrinksToExactlyThreeSteps()
    {
        // FailsAtThreeMachine throws when state >= 3, so the minimal
        // failing sequence is exactly 3 steps.
        // CommandSequenceShrinkPass registered in tier 0 must shrink to that minimum.
        TestRunResult result = await TestRunner.Run(
            new ConjectureSettings { MaxExamples = 200, UseDatabase = false },
            data =>
            {
                StateMachineStrategy<FailsAtThreeMachine, int, string> strategy = new(maxSteps: 50);
                _ = strategy.Generate(data);
            });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        Assert.Equal(3, CountCommandStarts(result.Counterexample!));
    }

    [Fact]
    public async Task Shrink_StateMachineFailure_ShrinkCountIsPositive()
    {
        TestRunResult result = await TestRunner.Run(
            new ConjectureSettings { MaxExamples = 200, UseDatabase = false },
            data =>
            {
                StateMachineStrategy<FailsAtThreeMachine, int, string> strategy = new(maxSteps: 50);
                _ = strategy.Generate(data);
            });

        Assert.False(result.Passed);
        Assert.True(result.ShrinkCount > 0);
    }

    [Fact]
    public async Task CommandSequencePass_NoCommandStartNodes_DoesNotBreakNonStatefulProperty()
    {
        // A non-stateful failing property (no CommandStart nodes) should still shrink
        // correctly — CommandSequenceShrinkPass returns false immediately for it.
        TestRunResult result = await TestRunner.Run(
            new ConjectureSettings { MaxExamples = 50, Seed = 1UL, UseDatabase = false },
            data =>
            {
                ulong v = data.NextInteger(0, 1000);
                if (v > 5)
                {
                    throw new Exception("too big");
                }
            });

        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        Assert.Equal(6UL, result.Counterexample![0].Value);
    }
}

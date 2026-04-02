// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.StateMachine;

public class CommandSequenceShrinkPass2Tests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Builds a minimal IR stream for N commands: [length, (CommandStart, cmdChoice=0) x N].</summary>
    private static List<IRNode> MakeCommandNodes(int commandCount)
    {
        List<IRNode> nodes = [IRNode.ForInteger((ulong)commandCount, 0, 50)];
        for (int i = 0; i < commandCount; i++)
        {
            nodes.Add(IRNode.ForCommandStart());
            nodes.Add(IRNode.ForInteger(0, 0, 0));
        }
        return nodes;
    }

    /// <summary>Builds a command sequence where each command choice is specified explicitly.</summary>
    private static List<IRNode> MakeCommandNodesWithChoices(ulong[] choices)
    {
        List<IRNode> nodes = [IRNode.ForInteger((ulong)choices.Length, 0, 50)];
        foreach (ulong choice in choices)
        {
            nodes.Add(IRNode.ForCommandStart());
            nodes.Add(IRNode.ForInteger(choice, 0, 99));
        }
        return nodes;
    }

    private static int CountCommandStarts(IReadOnlyList<IRNode> nodes)
    {
        int count = 0;
        foreach (IRNode node in nodes)
        {
            if (node.Kind == IRNodeKind.CommandStart)
            {
                count++;
            }
        }
        return count;
    }

    private static bool ContainsValue99(IReadOnlyList<IRNode> nodes)
    {
        foreach (IRNode node in nodes)
        {
            if (node.Kind == IRNodeKind.Integer && node.Value == 99UL)
            {
                return true;
            }
        }
        return false;
    }

    private static ShrinkState InterestingWhenCommandCountAtLeast(IReadOnlyList<IRNode> nodes, int minimum) =>
        new(nodes, n => new ValueTask<Status>(
            CountCommandStarts(n) >= minimum ? Status.Interesting : Status.Valid));

    private static ShrinkState InterestingWhenCommandCountIn(IReadOnlyList<IRNode> nodes, int a, int b) =>
        new(nodes, n =>
        {
            int c = CountCommandStarts(n);
            return new ValueTask<Status>(c == a || c == b ? Status.Interesting : Status.Valid);
        });

    private static ShrinkState InterestingWhenNoChoice99(IReadOnlyList<IRNode> nodes) =>
        new(nodes, n => new ValueTask<Status>(
            ContainsValue99(n) ? Status.Valid : Status.Interesting));

    // ─── Binary-halve tests ───────────────────────────────────────────────────

    [Fact]
    public async Task BinaryHalve_EightStepSequence_ShrinkesToFourInOneStep_WhenFirstFourSuffice()
    {
        // TruncateFromEnd produces 7 commands — not in {4, 8} → rejected.
        // BinaryHalve produces 4 commands — in {4, 8} → accepted.
        ShrinkState state = InterestingWhenCommandCountIn(MakeCommandNodes(8), 4, 8);
        CommandSequenceShrinkPass pass = new();

        bool result = await pass.TryReduce(state);

        Assert.True(result);
        Assert.Equal(4, CountCommandStarts(state.Nodes));
    }

    // ─── Delete-one tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteOne_RepeatedlyApplied_ConvergesToMinimalSingleStepSequence()
    {
        ShrinkState state = InterestingWhenCommandCountAtLeast(MakeCommandNodes(4), minimum: 1);
        CommandSequenceShrinkPass pass = new();

        bool progress;
        do
        {
            progress = await pass.TryReduce(state);
        } while (progress);

        Assert.Equal(1, CountCommandStarts(state.Nodes));
    }

    [Fact]
    public async Task DeleteOne_SkipsNonInterestingDeletion_ThenSucceedsOnValidDeletion()
    {
        // Choices: [0, 99, 0, 0]. 99 is in the first half so BinaryHalve ([0,99]) still has 99 → rejected.
        // TruncateFromEnd removes last → [0,99,0] → still has 99 → rejected.
        // DeleteOne cmd0: [cmd1=99, cmd2, cmd3] → has 99 → not interesting (skipped).
        // DeleteOne cmd1: [cmd0=0, cmd2=0, cmd3=0] → no 99 → interesting → accepted.
        ShrinkState state = InterestingWhenNoChoice99(MakeCommandNodesWithChoices([0UL, 99UL, 0UL, 0UL]));
        CommandSequenceShrinkPass pass = new();

        bool result = await pass.TryReduce(state);

        Assert.True(result);
        Assert.Equal(3, CountCommandStarts(state.Nodes));
        Assert.False(ContainsValue99(state.Nodes));
    }

    // ─── Idempotency test ─────────────────────────────────────────────────────

    [Fact]
    public async Task AfterConvergingToMinimum_RerunningShrinkPass_MakesNoFurtherProgress()
    {
        ShrinkState state = InterestingWhenCommandCountAtLeast(MakeCommandNodes(4), minimum: 1);
        CommandSequenceShrinkPass pass = new();

        bool progress;
        do
        {
            progress = await pass.TryReduce(state);
        } while (progress);

        bool furtherProgress = await pass.TryReduce(state);

        Assert.False(furtherProgress);
    }
}

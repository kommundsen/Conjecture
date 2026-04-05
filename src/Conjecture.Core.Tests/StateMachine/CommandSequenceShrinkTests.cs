// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.StateMachine;

public class CommandSequenceShrinkTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Builds a minimal IR stream for N commands: [length, (CommandStart, cmdChoice) x N].</summary>
    private static List<IRNode> MakeCommandNodes(int commandCount)
    {
        List<IRNode> nodes = [IRNode.ForInteger((ulong)commandCount, 0, 50)];
        for (int i = 0; i < commandCount; i++)
        {
            nodes.Add(IRNode.ForCommandStart());
            nodes.Add(IRNode.ForInteger(0, 0, 0)); // single-choice command
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

    private static ShrinkState AlwaysInteresting(IReadOnlyList<IRNode> nodes) =>
        new(nodes, n => new ValueTask<Status>(Status.Interesting));

    private static ShrinkState InterestingWhenCommandCountAtLeast(IReadOnlyList<IRNode> nodes, int minimum) =>
        new(nodes, n => new ValueTask<Status>(
            CountCommandStarts(n) >= minimum ? Status.Interesting : Status.Valid));

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TruncateFromEnd_NStepSequence_ReturnsTrueForNGreaterThanOne()
    {
        ShrinkState state = AlwaysInteresting(MakeCommandNodes(3));
        CommandSequenceShrinkPass pass = new();
        bool result = await pass.TryReduce(state);
        Assert.True(result);
    }

    [Fact]
    public async Task TruncateFromEnd_NStepSequence_ReducesToNMinusOneCommands()
    {
        ShrinkState state = AlwaysInteresting(MakeCommandNodes(3));
        CommandSequenceShrinkPass pass = new();
        await pass.TryReduce(state);
        Assert.Equal(2, CountCommandStarts(state.Nodes));
    }

    [Fact]
    public async Task TruncateFromEnd_LengthNodeIsDecrementedAfterTruncation()
    {
        ShrinkState state = AlwaysInteresting(MakeCommandNodes(2));
        CommandSequenceShrinkPass pass = new();
        await pass.TryReduce(state);
        Assert.Equal(1UL, state.Nodes[0].Value);
    }

    [Fact]
    public async Task SingleStepSequence_AtMinimum_ReturnsFalse()
    {
        // With count >= 1 as the minimum, a 1-step sequence cannot be reduced further.
        ShrinkState state = InterestingWhenCommandCountAtLeast(MakeCommandNodes(1), minimum: 1);
        CommandSequenceShrinkPass pass = new();
        bool result = await pass.TryReduce(state);
        Assert.False(result);
    }

    [Fact]
    public async Task TruncateFromEnd_NoCommandStartNodes_ReturnsFalse()
    {
        List<IRNode> nodes = [IRNode.ForInteger(0, 0, 50)];
        ShrinkState state = AlwaysInteresting(nodes);
        CommandSequenceShrinkPass pass = new();
        bool result = await pass.TryReduce(state);
        Assert.False(result);
    }

    [Fact]
    public async Task TruncateFromEnd_WhenCandidateNotInteresting_ReturnsFalse()
    {
        // The truncated (N-1) candidate is only interesting if there's >= 2 commands left
        // So with 2 commands, truncating to 1 is not interesting → returns false
        ShrinkState state = InterestingWhenCommandCountAtLeast(MakeCommandNodes(2), minimum: 2);
        CommandSequenceShrinkPass pass = new();
        bool result = await pass.TryReduce(state);
        Assert.False(result);
    }

    [Fact]
    public async Task TruncateFromEnd_AcceptsCandidate_WhenTryUpdateSucceeds()
    {
        // With 3 commands, truncating to 2 keeps >= 2 → interesting → accepted
        ShrinkState state = InterestingWhenCommandCountAtLeast(MakeCommandNodes(3), minimum: 2);
        CommandSequenceShrinkPass pass = new();
        bool result = await pass.TryReduce(state);
        Assert.True(result);
    }
}

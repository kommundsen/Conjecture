// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal.Shrinker;

public class DeleteBlocksPassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, n => new ValueTask<Status>(isInteresting(n)));

    private static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;

    [Fact]
    public async Task TryReduce_TwoNodes_DeletesOneAndReturnsTrue()
    {
        // Two integer nodes; deleting either still leaves a non-empty buffer.
        IRNode[] nodes =
        [
            IRNode.ForInteger(1, 0, 10),
            IRNode.ForInteger(2, 0, 10),
        ];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        DeleteBlocksPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Single(state.Nodes); // one node deleted
    }

    [Fact]
    public async Task TryReduce_SingleNode_CannotDeleteReturnsFalse()
    {
        // Deleting the only node leaves an empty buffer; if the predicate requires
        // at least one draw, the empty replay overruns and is not interesting.
        IRNode[] nodes = [IRNode.ForInteger(5, 0, 10)];
        // Predicate needs to draw — empty buffer causes Overrun → not interesting.
        static Status NeedsDraw(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try { data.NextInteger(0, 10); return Status.Interesting; }
            catch { return Status.Overrun; }
        }
        ShrinkState state = MakeState(nodes, NeedsDraw);
        DeleteBlocksPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Single(state.Nodes); // unchanged
    }

    [Fact]
    public async Task TryReduce_DeletionNotInteresting_ReturnsFalse()
    {
        // Predicate requires exactly two nodes; deleting either breaks it.
        IRNode[] nodes =
        [
            IRNode.ForInteger(3, 0, 10),
            IRNode.ForInteger(7, 0, 10),
        ];
        static Status NeedsTwo(IReadOnlyList<IRNode> ns) =>
            ns.Count == 2 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, NeedsTwo);
        DeleteBlocksPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(2, state.Nodes.Count); // unchanged
    }

    [Fact]
    public async Task TryReduce_DeletesPreservesRemainingNodeValues()
    {
        // Three nodes; first is deletable. Remaining two nodes must keep their values.
        IRNode[] nodes =
        [
            IRNode.ForInteger(9, 0, 10), // will be deleted
            IRNode.ForInteger(3, 0, 10),
            IRNode.ForInteger(4, 0, 10),
        ];
        // Interesting only when there are ≤2 nodes — triggers on first deletion.
        static Status InterestingWhenShorter(IReadOnlyList<IRNode> ns) =>
            ns.Count <= 2 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, InterestingWhenShorter);
        DeleteBlocksPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(2, state.Nodes.Count);
        // The two survivors keep their original values.
        Assert.Equal(3UL, state.Nodes[0].Value);
        Assert.Equal(4UL, state.Nodes[1].Value);
    }
}
using Conjecture.Core.Internal;
using Conjecture.Core.Internal.Shrinker;

namespace Conjecture.Tests.Internal.Shrinker;

public class IntervalDeletionPassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, isInteresting);

    private static IRNode Int(ulong v) => IRNode.ForInteger(v, 0, 10);

    [Fact]
    public void TryReduce_DeletesContiguousRunOfTwoNodes_InOneStep()
    {
        // 4 nodes; predicate interesting only when <= 2 nodes remain.
        // A single-node deletion leaves 3 (not interesting), so only an interval ≥2 works.
        var nodes = new[] { Int(1), Int(2), Int(3), Int(4) };
        static Status InterestingWhenTwo(IReadOnlyList<IRNode> ns) =>
            ns.Count <= 2 ? Status.Interesting : Status.Valid;
        var state = MakeState(nodes, InterestingWhenTwo);
        var pass = new IntervalDeletionPass();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.True(state.Nodes.Count <= 2);
    }

    [Fact]
    public void TryReduce_PreservesInterestingStatusAfterDeletion()
    {
        // After deleting an interval the remaining nodes still satisfy the predicate.
        var nodes = new[] { Int(5), Int(6), Int(7) };
        int calls = 0;
        Status Interesting(IReadOnlyList<IRNode> ns)
        {
            calls++;
            return ns.Count < 3 ? Status.Interesting : Status.Valid;
        }
        var state = MakeState(nodes, Interesting);
        var pass = new IntervalDeletionPass();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        // The predicate must have accepted the new nodes as interesting.
        Assert.True(state.Nodes.Count < 3);
    }

    [Fact]
    public void TryReduce_MoreAggressiveThanSingleNodeDeletion()
    {
        // Predicate: interesting only when exactly 1 node remains.
        // Deleting one node from 3 leaves 2 (not interesting) — DeleteBlocks fails.
        // An interval deletion of 2 from 3 leaves 1 (interesting) — IntervalDeletion succeeds.
        var nodes = new[] { Int(1), Int(2), Int(3) };
        static Status InterestingWhenOne(IReadOnlyList<IRNode> ns) =>
            ns.Count == 1 ? Status.Interesting : Status.Valid;

        // Verify DeleteBlocksPass cannot make progress.
        var deleteState = MakeState(nodes, InterestingWhenOne);
        bool deleteProgress = new DeleteBlocksPass().TryReduce(deleteState);
        Assert.False(deleteProgress);

        // IntervalDeletionPass should succeed where DeleteBlocksPass cannot.
        var intervalState = MakeState(nodes, InterestingWhenOne);
        bool intervalProgress = new IntervalDeletionPass().TryReduce(intervalState);
        Assert.True(intervalProgress);
        Assert.Single(intervalState.Nodes);
    }

    [Fact]
    public void TryReduce_Noop_WhenNoIntervalDeletionIsInteresting()
    {
        // Predicate requires exactly 5 nodes; any deletion makes it uninteresting.
        var nodes = new[] { Int(1), Int(2), Int(3), Int(4), Int(5) };
        static Status NeedsExactlyFive(IReadOnlyList<IRNode> ns) =>
            ns.Count == 5 ? Status.Interesting : Status.Valid;
        var state = MakeState(nodes, NeedsExactlyFive);
        var pass = new IntervalDeletionPass();

        bool progress = pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(5, state.Nodes.Count);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void TryReduce_HandlesIntervalSize(int intervalSize)
    {
        // Build a list that is exactly intervalSize+1 long.
        // Predicate: interesting only when 1 node remains, so only deleting `intervalSize`
        // nodes at once (the maximum matching interval) succeeds.
        IRNode[] nodes = new IRNode[intervalSize + 1];
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i] = Int((ulong)(i + 1));
        }
        static Status InterestingWhenOne(IReadOnlyList<IRNode> ns) =>
            ns.Count == 1 ? Status.Interesting : Status.Valid;
        var state = MakeState(nodes, InterestingWhenOne);
        var pass = new IntervalDeletionPass();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Single(state.Nodes);
    }
}

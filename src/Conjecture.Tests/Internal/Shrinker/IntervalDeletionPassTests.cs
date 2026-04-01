using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal.Shrinker;

public class IntervalDeletionPassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, n => new ValueTask<Status>(isInteresting(n)));

    private static IRNode Int(ulong v) => IRNode.ForInteger(v, 0, 10);

    [Fact]
    public async Task TryReduce_DeletesContiguousRunOfTwoNodes_InOneStep()
    {
        // 4 nodes; predicate interesting only when <= 2 nodes remain.
        // A single-node deletion leaves 3 (not interesting), so only an interval ≥2 works.
        IRNode[] nodes = [Int(1), Int(2), Int(3), Int(4)];
        static Status InterestingWhenTwo(IReadOnlyList<IRNode> ns) =>
            ns.Count <= 2 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, InterestingWhenTwo);
        IntervalDeletionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.True(state.Nodes.Count <= 2);
    }

    [Fact]
    public async Task TryReduce_PreservesInterestingStatusAfterDeletion()
    {
        // After deleting an interval the remaining nodes still satisfy the predicate.
        IRNode[] nodes = [Int(5), Int(6), Int(7)];
        int calls = 0;
        Status Interesting(IReadOnlyList<IRNode> ns)
        {
            calls++;
            return ns.Count < 3 ? Status.Interesting : Status.Valid;
        }
        ShrinkState state = MakeState(nodes, Interesting);
        IntervalDeletionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        // The predicate must have accepted the new nodes as interesting.
        Assert.True(state.Nodes.Count < 3);
    }

    [Fact]
    public async Task TryReduce_MoreAggressiveThanSingleNodeDeletion()
    {
        // Predicate: interesting only when exactly 1 node remains.
        // Deleting one node from 3 leaves 2 (not interesting) — DeleteBlocks fails.
        // An interval deletion of 2 from 3 leaves 1 (interesting) — IntervalDeletion succeeds.
        IRNode[] nodes = [Int(1), Int(2), Int(3)];
        static Status InterestingWhenOne(IReadOnlyList<IRNode> ns) =>
            ns.Count == 1 ? Status.Interesting : Status.Valid;

        // Verify DeleteBlocksPass cannot make progress.
        ShrinkState deleteState = MakeState(nodes, InterestingWhenOne);
        bool deleteProgress = await new DeleteBlocksPass().TryReduce(deleteState);
        Assert.False(deleteProgress);

        // IntervalDeletionPass should succeed where DeleteBlocksPass cannot.
        ShrinkState intervalState = MakeState(nodes, InterestingWhenOne);
        bool intervalProgress = await new IntervalDeletionPass().TryReduce(intervalState);
        Assert.True(intervalProgress);
        Assert.Single(intervalState.Nodes);
    }

    [Fact]
    public async Task TryReduce_Noop_WhenNoIntervalDeletionIsInteresting()
    {
        // Predicate requires exactly 5 nodes; any deletion makes it uninteresting.
        IRNode[] nodes = [Int(1), Int(2), Int(3), Int(4), Int(5)];
        static Status NeedsExactlyFive(IReadOnlyList<IRNode> ns) =>
            ns.Count == 5 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, NeedsExactlyFive);
        IntervalDeletionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(5, state.Nodes.Count);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task TryReduce_HandlesIntervalSize(int intervalSize)
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
        ShrinkState state = MakeState(nodes, InterestingWhenOne);
        IntervalDeletionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Single(state.Nodes);
    }
}

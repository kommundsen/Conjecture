using Conjecture.Core.Internal;
using Conjecture.Core.Internal.Shrinker;

namespace Conjecture.Tests.Internal.Shrinker;

public class IntegerReductionPassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, n => new ValueTask<Status>(isInteresting(n)));

    private static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;

    [Fact]
    public async Task TryReduce_LargeInteger_BinarySearchesToSmallestInterestingValue()
    {
        // Value 1000, threshold 5 — binary search should land exactly at 5.
        IRNode[] nodes = [IRNode.ForInteger(1000, 0, 2000)];
        static Status AtLeastFive(IReadOnlyList<IRNode> ns) =>
            ns[0].Value >= 5 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, AtLeastFive);
        IntegerReductionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(5UL, state.Nodes[0].Value);
    }

    [Fact]
    public async Task TryReduce_NodeAtMin_SkipsAndReturnsFalse()
    {
        IRNode[] nodes = [IRNode.ForInteger(0, 0, 100)];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        IntegerReductionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(0UL, state.Nodes[0].Value);
    }

    [Fact]
    public async Task TryReduce_ReductionNotInteresting_ReturnsFalse()
    {
        // Only the original value satisfies the predicate.
        IRNode[] nodes = [IRNode.ForInteger(42, 0, 100)];
        static Status OnlyFortyTwo(IReadOnlyList<IRNode> ns) =>
            ns[0].Value == 42 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, OnlyFortyTwo);
        IntegerReductionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(42UL, state.Nodes[0].Value);
    }

    [Fact]
    public async Task TryReduce_NodeWithNonZeroMin_SearchStaysAboveMin()
    {
        // Min is 10, value is 100, threshold is 20 — result must be in [20, 100).
        IRNode[] nodes = [IRNode.ForInteger(100, 10, 200)];
        static Status AtLeastTwenty(IReadOnlyList<IRNode> ns) =>
            ns[0].Value >= 20 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, AtLeastTwenty);
        IntegerReductionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        ulong v = state.Nodes[0].Value;
        Assert.True(v >= 20 && v < 100, $"Expected value in [20,100), got {v}");
    }

    [Fact]
    public async Task TryReduce_MultipleNodes_EachReducedToMinimum()
    {
        // Two independent nodes, each with their own threshold.
        IRNode[] nodes =
        [
            IRNode.ForInteger(500, 0, 1000),
            IRNode.ForInteger(800, 0, 1000),
        ];
        static Status Thresholds(IReadOnlyList<IRNode> ns) =>
            ns[0].Value >= 3 && ns[1].Value >= 7 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, Thresholds);
        IntegerReductionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(3UL, state.Nodes[0].Value);
        Assert.Equal(7UL, state.Nodes[1].Value);
    }
}

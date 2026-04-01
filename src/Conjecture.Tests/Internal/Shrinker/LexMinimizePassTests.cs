using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal.Shrinker;

public class LexMinimizePassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, n => new ValueTask<Status>(isInteresting(n)));

    private static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;

    [Fact]
    public async Task TryReduce_NodeAboveMin_ReducesValueAndReturnsTrue()
    {
        // Node with value 10 — pass should reduce it toward min (0).
        IRNode[] nodes = [IRNode.ForInteger(10, 0, 100)];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        LexMinimizePass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.True(state.Nodes[0].Value < 10, $"Expected value < 10, got {state.Nodes[0].Value}");
    }

    [Fact]
    public async Task TryReduce_NodeAtMin_SkipsAndReturnsFalse()
    {
        // Node already at minimum — nothing to reduce.
        IRNode[] nodes = [IRNode.ForInteger(0, 0, 100)];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        LexMinimizePass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(0UL, state.Nodes[0].Value);
    }

    [Fact]
    public async Task TryReduce_ReductionNotInteresting_ReturnsFalse()
    {
        // Predicate requires the exact original value — any reduction breaks it.
        IRNode[] nodes = [IRNode.ForInteger(5, 0, 10)];
        static Status OnlyFive(IReadOnlyList<IRNode> ns) =>
            ns[0].Value == 5 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, OnlyFive);
        LexMinimizePass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(5UL, state.Nodes[0].Value); // unchanged
    }

    [Fact]
    public async Task TryReduce_NodeWithNonZeroMin_ReducesTowardMin()
    {
        // Min is 3, value is 8 — reduction must stay >= min.
        IRNode[] nodes = [IRNode.ForInteger(8, 3, 20)];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        LexMinimizePass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        ulong v = state.Nodes[0].Value;
        Assert.True(v >= 3 && v < 8, $"Expected value in [3,8), got {v}");
    }

    [Fact]
    public async Task TryReduce_MultipleNodes_AllReducibleNodesAreDecremented()
    {
        // Two nodes both above min with always-interesting predicate.
        // Both should be reduced in a single pass.
        IRNode[] nodes =
        [
            IRNode.ForInteger(6, 0, 10),
            IRNode.ForInteger(9, 0, 10),
        ];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        LexMinimizePass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.True(state.Nodes[0].Value < 6);
        Assert.True(state.Nodes[1].Value < 9);
    }

    [Fact]
    public async Task TryReduce_AllNodesAtMin_ReturnsFalse()
    {
        // Every node is already at its minimum value.
        IRNode[] nodes =
        [
            IRNode.ForInteger(2, 2, 10),
            IRNode.ForInteger(5, 5, 10),
        ];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        LexMinimizePass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(2UL, state.Nodes[0].Value);
        Assert.Equal(5UL, state.Nodes[1].Value);
    }
}

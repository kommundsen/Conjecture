using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal.Shrinker;

public class ZeroBlocksPassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, n => new ValueTask<Status>(isInteresting(n)));

    // Always interesting — any buffer satisfies the predicate.
    private static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;

    [Fact]
    public async Task TryReduce_IntegerNodeAboveMin_ZeroesNodeAndReturnsTrue()
    {
        // Node with value 42, min 0 — zeroing to min should succeed.
        IRNode[] nodes = [IRNode.ForInteger(42, 0, 100)];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        ZeroBlocksPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(0UL, state.Nodes[0].Value);
    }

    [Fact]
    public async Task TryReduce_IntegerNodeAtMin_SkipsNodeAndReturnsFalse()
    {
        // Node already at minimum — nothing to zero, no progress.
        IRNode[] nodes = [IRNode.ForInteger(0, 0, 100)];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        ZeroBlocksPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(0UL, state.Nodes[0].Value);
    }

    [Fact]
    public async Task TryReduce_ZeroingNotInteresting_ReturnsFalse()
    {
        // Predicate only accepts original value — zeroing would break it, so no progress.
        IRNode[] nodes = [IRNode.ForInteger(99, 0, 100)];
        static Status OnlyOriginal(IReadOnlyList<IRNode> ns) =>
            ns[0].Value == 99 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, OnlyOriginal);
        ZeroBlocksPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(99UL, state.Nodes[0].Value); // unchanged
    }

    [Fact]
    public async Task TryReduce_MultipleNodes_ZeroesFirstReducibleNode()
    {
        // First node already at min; second node can be zeroed.
        IRNode[] nodes =
        [
            IRNode.ForInteger(0, 0, 10),   // already at min
            IRNode.ForInteger(7, 0, 10),   // can be zeroed
        ];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        ZeroBlocksPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(0UL, state.Nodes[0].Value); // unchanged
        Assert.Equal(0UL, state.Nodes[1].Value); // zeroed
    }

    [Fact]
    public async Task TryReduce_NonIntegerNodes_IgnoredReturnsFalse()
    {
        // Boolean node — ZeroBlocks operates only on integers; nothing to do.
        IRNode[] nodes = [IRNode.ForBoolean(true)];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        ZeroBlocksPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_IntegerNodeWithNonZeroMin_ZeroesToMin()
    {
        // Min is 5, value is 50 — "zeroing" means reducing to Min, not literally 0.
        IRNode[] nodes = [IRNode.ForInteger(50, 5, 100)];
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        ZeroBlocksPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(5UL, state.Nodes[0].Value);
    }
}

using Conjecture.Core.Internal;
using Conjecture.Core.Internal.Shrinker;

namespace Conjecture.Tests.Internal.Shrinker;

public class ZeroBlocksPassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, isInteresting);

    // Always interesting — any buffer satisfies the predicate.
    private static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;

    [Fact]
    public void TryReduce_IntegerNodeAboveMin_ZeroesNodeAndReturnsTrue()
    {
        // Node with value 42, min 0 — zeroing to min should succeed.
        var nodes = new[] { IRNode.ForInteger(42, 0, 100) };
        var state = MakeState(nodes, AlwaysInteresting);
        var pass = new ZeroBlocksPass();

        var progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(0UL, state.Nodes[0].Value);
    }

    [Fact]
    public void TryReduce_IntegerNodeAtMin_SkipsNodeAndReturnsFalse()
    {
        // Node already at minimum — nothing to zero, no progress.
        var nodes = new[] { IRNode.ForInteger(0, 0, 100) };
        var state = MakeState(nodes, AlwaysInteresting);
        var pass = new ZeroBlocksPass();

        var progress = pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(0UL, state.Nodes[0].Value);
    }

    [Fact]
    public void TryReduce_ZeroingNotInteresting_ReturnsFalse()
    {
        // Predicate only accepts original value — zeroing would break it, so no progress.
        var nodes = new[] { IRNode.ForInteger(99, 0, 100) };
        Func<IReadOnlyList<IRNode>, Status> onlyOriginal =
            ns => ns[0].Value == 99 ? Status.Interesting : Status.Valid;
        var state = MakeState(nodes, onlyOriginal);
        var pass = new ZeroBlocksPass();

        var progress = pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(99UL, state.Nodes[0].Value); // unchanged
    }

    [Fact]
    public void TryReduce_MultipleNodes_ZeroesFirstReducibleNode()
    {
        // First node already at min; second node can be zeroed.
        var nodes = new[]
        {
            IRNode.ForInteger(0, 0, 10),   // already at min
            IRNode.ForInteger(7, 0, 10),   // can be zeroed
        };
        var state = MakeState(nodes, AlwaysInteresting);
        var pass = new ZeroBlocksPass();

        var progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(0UL, state.Nodes[0].Value); // unchanged
        Assert.Equal(0UL, state.Nodes[1].Value); // zeroed
    }

    [Fact]
    public void TryReduce_NonIntegerNodes_IgnoredReturnsFalse()
    {
        // Boolean node — ZeroBlocks operates only on integers; nothing to do.
        var nodes = new[] { IRNode.ForBoolean(true) };
        var state = MakeState(nodes, AlwaysInteresting);
        var pass = new ZeroBlocksPass();

        var progress = pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public void TryReduce_IntegerNodeWithNonZeroMin_ZeroesToMin()
    {
        // Min is 5, value is 50 — "zeroing" means reducing to Min, not literally 0.
        var nodes = new[] { IRNode.ForInteger(50, 5, 100) };
        var state = MakeState(nodes, AlwaysInteresting);
        var pass = new ZeroBlocksPass();

        var progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(5UL, state.Nodes[0].Value);
    }
}

using Conjecture.Core.Internal;
using Conjecture.Core.Internal.Shrinker;

namespace Conjecture.Tests.Internal.Shrinker;

public class BlockSwappingPassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, n => new ValueTask<Status>(isInteresting(n)));

    private static IRNode Int(ulong v) => IRNode.ForInteger(v, 0, 10);

    [Fact]
    public async Task TryReduce_SwapsAdjacentIntegerNodes_WhenSwapIsInteresting()
    {
        // [5, 3] → try [3, 5]; predicate accepts any ordering so the swap succeeds.
        IRNode[] nodes = [Int(5), Int(3)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        BlockSwappingPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(2, state.Nodes.Count);
        Assert.True(state.Nodes[0].Value <= state.Nodes[1].Value,
            "Nodes should be in non-decreasing order after swap.");
    }

    [Fact]
    public async Task TryReduce_Noop_WhenNodesAlreadyInAscendingOrder()
    {
        IRNode[] nodes = [Int(1), Int(2), Int(3)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        BlockSwappingPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_PreservesInterestingStatusAfterSwap()
    {
        // Predicate: interesting only when first node value ≤ second node value.
        IRNode[] nodes = [Int(7), Int(2)];
        static Status InterestingWhenSorted(IReadOnlyList<IRNode> ns) =>
            ns.Count == 2 && ns[0].Value <= ns[1].Value ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, InterestingWhenSorted);
        BlockSwappingPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(2UL, state.Nodes[0].Value);
        Assert.Equal(7UL, state.Nodes[1].Value);
    }

    [Fact]
    public async Task TryReduce_DoesNotSwap_IntegerAndBooleanNodes()
    {
        // Integer(5) adjacent to Boolean(0): different kinds — must not be swapped.
        IRNode[] nodes = [Int(5), IRNode.ForBoolean(false)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        BlockSwappingPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_DoesNotSwap_IntegerAndFloat64Nodes()
    {
        // Integer(5) adjacent to Float64(3): different kinds — must not be swapped.
        IRNode[] nodes = [Int(5), IRNode.ForFloat64(3, 0, 10)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        BlockSwappingPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }
}

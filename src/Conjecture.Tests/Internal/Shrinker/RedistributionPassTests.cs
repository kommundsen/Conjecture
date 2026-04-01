using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal.Shrinker;

public class RedistributionPassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, n => new ValueTask<Status>(isInteresting(n)));

    private static IRNode Int(ulong v, ulong max = 10) => IRNode.ForInteger(v, 0, max);

    [Fact]
    public async Task TryReduce_ReducesLeftValue_WhenShiftPreservesInteresting()
    {
        // (5, 3) → should find a candidate with left < 5 by shifting magnitude to right.
        IRNode[] nodes = [Int(5), Int(3)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        RedistributionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.True(state.Nodes[0].Value < 5UL, "Left node value should decrease after redistribution.");
    }

    [Fact]
    public async Task TryReduce_FindsLexicographicallySmallestPair_WhenMultipleCandidatesAreInteresting()
    {
        // Predicate: only interesting when left == 2, right == 6 (shift of 3 from (5, 3)).
        // A smaller left value (2) should be preferred over (4, 4) or (3, 5).
        IRNode[] nodes = [Int(5), Int(3)];
        static Status OnlyTwoAndSix(IReadOnlyList<IRNode> ns) =>
            ns.Count == 2 && ns[0].Value == 2 && ns[1].Value == 6 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, OnlyTwoAndSix);
        RedistributionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(2UL, state.Nodes[0].Value);
        Assert.Equal(6UL, state.Nodes[1].Value);
    }

    [Fact]
    public async Task TryReduce_Noop_WhenLeftNodeAtMin()
    {
        // (0, 5): left is already at min 0 — no shift possible.
        IRNode[] nodes = [Int(0), Int(5)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        RedistributionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_Noop_WhenRightNodeAtMax()
    {
        // (5, 10) where max=10: any shift would push right above max — no valid candidate.
        IRNode[] nodes = [Int(5), Int(10)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        RedistributionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_Noop_WhenNoShiftPreservesInteresting()
    {
        // Predicate accepts only the original (5, 3): no redistribution is accepted.
        IRNode[] nodes = [Int(5), Int(3)];
        static Status OnlyOriginal(IReadOnlyList<IRNode> ns) =>
            ns.Count == 2 && ns[0].Value == 5 && ns[1].Value == 3 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, OnlyOriginal);
        RedistributionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_SkipsBooleanNodes()
    {
        // Boolean(true) adjacent to Integer(5): not an integer pair — no redistribution.
        IRNode[] nodes = [IRNode.ForBoolean(true), Int(5)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        RedistributionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_SkipsBytesNodes()
    {
        // Bytes adjacent to Integer: not an integer pair — no redistribution.
        IRNode[] nodes = [IRNode.ForBytes(5), Int(3)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        RedistributionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_Noop_WhenIntegerPairSeparatedByNonIntegerNode()
    {
        // Integer(5), Boolean(true), Integer(3): the two integers are not adjacent — no redistribution.
        IRNode[] nodes = [Int(5), IRNode.ForBoolean(true), Int(3)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        RedistributionPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }
}

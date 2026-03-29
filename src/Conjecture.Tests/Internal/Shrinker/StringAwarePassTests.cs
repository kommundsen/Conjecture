using Conjecture.Core.Internal;
using Conjecture.Core.Internal.Shrinker;

namespace Conjecture.Tests.Internal.Shrinker;

public class StringAwarePassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, isInteresting);

    private static IRNode Len(ulong value, ulong max = 10) =>
        IRNode.ForStringLength(value, 0, max);

    private static IRNode Ch(ulong codepoint) =>
        IRNode.ForStringChar(codepoint, 32, 127);

    // ── Atomic length reduction + char deletion ───────────────────────────────

    [Fact]
    public void TryReduce_ReducesStringLength_AndDeletesTrailingCharAtomically()
    {
        // 2-char string. Predicate requires: declared length == actual char count && length < 2.
        // If the pass reduces length WITHOUT removing a char (e.g. [Len(1), Ch('b'), Ch('c')]),
        // the count mismatch makes the predicate return Valid — forcing the real atomic operation.
        var nodes = new IRNode[] { Len(2), Ch('b'), Ch('c') };
        static Status ConsistentAndShorter(IReadOnlyList<IRNode> ns)
        {
            int charCount = 0;
            ulong declared = 0;
            foreach (IRNode n in ns)
            {
                if (n.Kind == IRNodeKind.StringLength) { declared = n.Value; }
                else if (n.Kind == IRNodeKind.StringChar) { charCount++; }
            }
            return charCount == (int)declared && declared < 2 ? Status.Interesting : Status.Valid;
        }
        ShrinkState state = MakeState(nodes, ConsistentAndShorter);
        StringAwarePass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(1UL, state.Nodes.First(n => n.Kind == IRNodeKind.StringLength).Value);
        Assert.Equal(1, state.Nodes.Count(n => n.Kind == IRNodeKind.StringChar));
    }

    // ── Multi-character deletion ──────────────────────────────────────────────

    [Fact]
    public void TryReduce_MultiCharDeletion_ReducesToEmptyStringOverIterations()
    {
        // 3-char string; interesting only when the string is empty (length == 0).
        var nodes = new IRNode[] { Len(3), Ch('x'), Ch('y'), Ch('z') };
        static Status InterestingWhenEmpty(IReadOnlyList<IRNode> ns) =>
            ns.First(n => n.Kind == IRNodeKind.StringLength).Value == 0
                ? Status.Interesting
                : Status.Valid;
        ShrinkState state = MakeState(nodes, InterestingWhenEmpty);
        StringAwarePass pass = new();

        while (pass.TryReduce(state)) { }

        Assert.Equal(0UL, state.Nodes.First(n => n.Kind == IRNodeKind.StringLength).Value);
        Assert.Equal(0, state.Nodes.Count(n => n.Kind == IRNodeKind.StringChar));
    }

    // ── Character simplification toward 'a' ──────────────────────────────────

    [Fact]
    public void TryReduce_StringCharNode_SimplifiesToA()
    {
        // 1-char string with 'z' (122). Predicate accepts when the char becomes 'a' (97).
        var nodes = new IRNode[] { Len(1), Ch('z') };
        static Status InterestingWhenA(IReadOnlyList<IRNode> ns) =>
            ns.Any(n => n.Kind == IRNodeKind.StringChar && n.Value == 'a')
                ? Status.Interesting
                : Status.Valid;
        ShrinkState state = MakeState(nodes, InterestingWhenA);
        StringAwarePass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal((ulong)'a', state.Nodes.First(n => n.Kind == IRNodeKind.StringChar).Value);
    }

    [Fact]
    public void TryReduce_StringCharNode_SimplifiesToSpace_WhenANotInteresting()
    {
        // 1-char string with 'z' (122). Only space (32) is accepted.
        var nodes = new IRNode[] { Len(1), Ch('z') };
        static Status InterestingWhenSpace(IReadOnlyList<IRNode> ns) =>
            ns.Any(n => n.Kind == IRNodeKind.StringChar && n.Value == 32)
                ? Status.Interesting
                : Status.Valid;
        ShrinkState state = MakeState(nodes, InterestingWhenSpace);
        StringAwarePass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(32UL, state.Nodes.First(n => n.Kind == IRNodeKind.StringChar).Value);
    }

    // ── Failure preserved after simplification ────────────────────────────────

    [Fact]
    public void TryReduce_PreservesInterestingStatusAfterSimplification()
    {
        // 2-char string "zz". Predicate: interesting when at least one char is 'a'.
        // After simplification the state must still satisfy the predicate.
        var nodes = new IRNode[] { Len(2), Ch('z'), Ch('z') };
        static Status CountingPredicate(IReadOnlyList<IRNode> ns) =>
            ns.Any(n => n.Kind == IRNodeKind.StringChar && n.Value == 'a')
                ? Status.Interesting
                : Status.Valid;
        ShrinkState state = MakeState(nodes, CountingPredicate);
        StringAwarePass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Contains(state.Nodes, n => n.Kind == IRNodeKind.StringChar && n.Value == 'a');
    }

    // ── Kind filter ───────────────────────────────────────────────────────────

    [Fact]
    public void TryReduce_IntegerNodesOnly_IsNoOp()
    {
        var nodes = new IRNode[] { IRNode.ForInteger(10, 0, 100), IRNode.ForInteger(20, 0, 100) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        StringAwarePass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public void TryReduce_EmptyString_IsNoOp()
    {
        // StringLength(0) with no char nodes — nothing left to simplify.
        var nodes = new IRNode[] { Len(0) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        StringAwarePass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public void TryReduce_CharAlreadyA_NoCharSimplification()
    {
        // 1-char string whose char is already 'a'. No char simplification possible.
        // The pass may still try length reduction, but if the predicate rejects it we get false.
        var nodes = new IRNode[] { Len(1, max: 1), Ch('a') };
        // Only accept this exact state: length==1, char=='a'
        static Status OnlyOriginal(IReadOnlyList<IRNode> ns) =>
            ns.Count == 2 && ns[0].Value == 1 && ns[1].Value == 'a'
                ? Status.Interesting
                : Status.Valid;
        ShrinkState state = MakeState(nodes, OnlyOriginal);
        StringAwarePass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.False(progress);
    }
}

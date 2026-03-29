using System.Runtime.CompilerServices;
using Conjecture.Core.Internal;
using Conjecture.Core.Internal.Shrinker;

namespace Conjecture.Tests.Internal.Shrinker;

public class FloatSimplificationPassTests
{
    private static readonly ulong ZeroBits64 = Unsafe.BitCast<double, ulong>(0.0);
    private static readonly ulong NaNBits64 = Unsafe.BitCast<double, ulong>(double.NaN);
    private static readonly ulong PosInfBits64 = Unsafe.BitCast<double, ulong>(double.PositiveInfinity);
    private static readonly ulong NegInfBits64 = Unsafe.BitCast<double, ulong>(double.NegativeInfinity);
    private static readonly ulong MaxFiniteBits64 = Unsafe.BitCast<double, ulong>(double.MaxValue);
    private static readonly ulong MinFiniteBits64 = Unsafe.BitCast<double, ulong>(double.MinValue);
    private static readonly ulong NegFiveBits64 = Unsafe.BitCast<double, ulong>(-5.0);
    private static readonly ulong PosFiveBits64 = Unsafe.BitCast<double, ulong>(5.0);

    private static readonly ulong ZeroBits32 = Unsafe.BitCast<float, uint>(0f);
    private static readonly ulong NaNBits32 = Unsafe.BitCast<float, uint>(float.NaN);

    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, isInteresting);

    private static IRNode Float64(ulong bits) =>
        IRNode.ForFloat64(bits, 0UL, ulong.MaxValue);

    private static IRNode Float32(ulong bits) =>
        IRNode.ForFloat32(bits, 0UL, uint.MaxValue);

    // ── NaN ──────────────────────────────────────────────────────────────────

    [Fact]
    public void TryReduce_NaN64_SimplifiesToZero()
    {
        var nodes = new[] { Float64(NaNBits64) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(ZeroBits64, state.Nodes[0].Value);
    }

    [Fact]
    public void TryReduce_NaN32_SimplifiesToZero()
    {
        var nodes = new[] { Float32(NaNBits32) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(ZeroBits32, state.Nodes[0].Value);
    }

    // ── Infinity ─────────────────────────────────────────────────────────────

    [Fact]
    public void TryReduce_PositiveInfinity_SimplifiesToMaxFiniteOrZero()
    {
        var nodes = new[] { Float64(PosInfBits64) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        double result = Unsafe.BitCast<ulong, double>(state.Nodes[0].Value);
        Assert.True(!double.IsInfinity(result), "PositiveInfinity should be reduced to a finite value.");
    }

    [Fact]
    public void TryReduce_PositiveInfinity_EventuallyReachesZero()
    {
        // Run TryReduce until no more progress; the result must be 0.0 (or at least finite & non-negative).
        var nodes = new[] { Float64(PosInfBits64) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        while (pass.TryReduce(state)) { }

        Assert.Equal(ZeroBits64, state.Nodes[0].Value);
    }

    [Fact]
    public void TryReduce_NegativeInfinity_SimplifiesToFiniteOrZero()
    {
        var nodes = new[] { Float64(NegInfBits64) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        double result = Unsafe.BitCast<ulong, double>(state.Nodes[0].Value);
        Assert.True(!double.IsInfinity(result), "NegativeInfinity should be reduced to a finite value.");
    }

    // ── Negative float ───────────────────────────────────────────────────────

    [Fact]
    public void TryReduce_NegativeFloat_TriesPositiveEquivalent()
    {
        var nodes = new[] { Float64(NegFiveBits64) };
        // Accept only the positive equivalent.
        static Status OnlyPositive(IReadOnlyList<IRNode> ns) =>
            ns[0].Value == PosFiveBits64 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, OnlyPositive);
        FloatSimplificationPass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(PosFiveBits64, state.Nodes[0].Value);
    }

    // ── Large float → smaller ────────────────────────────────────────────────

    [Fact]
    public void TryReduce_LargeFloat_ReducesMagnitudeTowardZero()
    {
        ulong largeBits = Unsafe.BitCast<double, ulong>(1e200);
        var nodes = new[] { Float64(largeBits) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.True(progress);
        double original = Unsafe.BitCast<ulong, double>(largeBits);
        double result = Unsafe.BitCast<ulong, double>(state.Nodes[0].Value);
        Assert.True(Math.Abs(result) < Math.Abs(original),
            $"Expected |result| {Math.Abs(result)} < |original| {Math.Abs(original)}.");
    }

    [Fact]
    public void TryReduce_LargeFloat_EventuallyReachesZero()
    {
        ulong largeBits = Unsafe.BitCast<double, ulong>(1e100);
        var nodes = new[] { Float64(largeBits) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        while (pass.TryReduce(state)) { }

        Assert.Equal(ZeroBits64, state.Nodes[0].Value);
    }

    // ── Kind filter ──────────────────────────────────────────────────────────

    [Fact]
    public void TryReduce_IntegerNode_IsNoOp()
    {
        // An Integer node with a large value that looks like a NaN bit pattern — must not be touched.
        var nodes = new[] { IRNode.ForInteger(NaNBits64, 0UL, ulong.MaxValue) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public void TryReduce_AlreadyZero_IsNoOp()
    {
        // 0.0 is already the simplest float — nothing to do.
        var nodes = new[] { Float64(ZeroBits64) };
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = pass.TryReduce(state);

        Assert.False(progress);
    }
}

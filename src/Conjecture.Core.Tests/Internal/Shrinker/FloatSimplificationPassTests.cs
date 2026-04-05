// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Runtime.CompilerServices;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal.Shrinker;

public class FloatSimplificationPassTests
{
    private static readonly ulong ZeroBits64 = Unsafe.BitCast<double, ulong>(0.0);
    private static readonly ulong NaNBits64 = Unsafe.BitCast<double, ulong>(double.NaN);
    private static readonly ulong PosInfBits64 = Unsafe.BitCast<double, ulong>(double.PositiveInfinity);
    private static readonly ulong NegInfBits64 = Unsafe.BitCast<double, ulong>(double.NegativeInfinity);
    private static readonly ulong NegFiveBits64 = Unsafe.BitCast<double, ulong>(-5.0);
    private static readonly ulong PosFiveBits64 = Unsafe.BitCast<double, ulong>(5.0);

    private static readonly ulong ZeroBits32 = Unsafe.BitCast<float, uint>(0f);
    private static readonly ulong NaNBits32 = Unsafe.BitCast<float, uint>(float.NaN);

    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, n => new ValueTask<Status>(isInteresting(n)));

    private static IRNode Float64(ulong bits) =>
        IRNode.ForFloat64(bits, 0UL, ulong.MaxValue);

    private static IRNode Float32(ulong bits) =>
        IRNode.ForFloat32(bits, 0UL, uint.MaxValue);

    // ── NaN ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryReduce_NaN64_SimplifiesToZero()
    {
        IRNode[] nodes = [Float64(NaNBits64)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(ZeroBits64, state.Nodes[0].Value);
    }

    [Fact]
    public async Task TryReduce_NaN32_SimplifiesToZero()
    {
        IRNode[] nodes = [Float32(NaNBits32)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(ZeroBits32, state.Nodes[0].Value);
    }

    // ── Infinity ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryReduce_PositiveInfinity_SimplifiesToMaxFiniteOrZero()
    {
        IRNode[] nodes = [Float64(PosInfBits64)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        double result = Unsafe.BitCast<ulong, double>(state.Nodes[0].Value);
        Assert.True(!double.IsInfinity(result), "PositiveInfinity should be reduced to a finite value.");
    }

    [Fact]
    public async Task TryReduce_PositiveInfinity_EventuallyReachesZero()
    {
        // Run TryReduce until no more progress; the result must be 0.0 (or at least finite & non-negative).
        IRNode[] nodes = [Float64(PosInfBits64)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        while (await pass.TryReduce(state)) { }

        Assert.Equal(ZeroBits64, state.Nodes[0].Value);
    }

    [Fact]
    public async Task TryReduce_NegativeInfinity_SimplifiesToFiniteOrZero()
    {
        IRNode[] nodes = [Float64(NegInfBits64)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        double result = Unsafe.BitCast<ulong, double>(state.Nodes[0].Value);
        Assert.True(!double.IsInfinity(result), "NegativeInfinity should be reduced to a finite value.");
    }

    // ── Negative float ───────────────────────────────────────────────────────

    [Fact]
    public async Task TryReduce_NegativeFloat_TriesPositiveEquivalent()
    {
        IRNode[] nodes = [Float64(NegFiveBits64)];
        // Accept only the positive equivalent.
        static Status OnlyPositive(IReadOnlyList<IRNode> ns) =>
            ns[0].Value == PosFiveBits64 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, OnlyPositive);
        FloatSimplificationPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(PosFiveBits64, state.Nodes[0].Value);
    }

    // ── Large float → smaller ────────────────────────────────────────────────

    [Fact]
    public async Task TryReduce_LargeFloat_ReducesMagnitudeTowardZero()
    {
        ulong largeBits = Unsafe.BitCast<double, ulong>(1e200);
        IRNode[] nodes = [Float64(largeBits)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        double original = Unsafe.BitCast<ulong, double>(largeBits);
        double result = Unsafe.BitCast<ulong, double>(state.Nodes[0].Value);
        Assert.True(Math.Abs(result) < Math.Abs(original),
            $"Expected |result| {Math.Abs(result)} < |original| {Math.Abs(original)}.");
    }

    [Fact]
    public async Task TryReduce_LargeFloat_EventuallyReachesZero()
    {
        ulong largeBits = Unsafe.BitCast<double, ulong>(1e100);
        IRNode[] nodes = [Float64(largeBits)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        while (await pass.TryReduce(state)) { }

        Assert.Equal(ZeroBits64, state.Nodes[0].Value);
    }

    // ── Kind filter ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TryReduce_IntegerNode_IsNoOp()
    {
        // An Integer node with a large value that looks like a NaN bit pattern — must not be touched.
        IRNode[] nodes = [IRNode.ForInteger(NaNBits64, 0UL, ulong.MaxValue)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_AlreadyZero_IsNoOp()
    {
        // 0.0 is already the simplest float — nothing to do.
        IRNode[] nodes = [Float64(ZeroBits64)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        FloatSimplificationPass pass = new();

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }
}
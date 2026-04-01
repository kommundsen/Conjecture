using System.Buffers;

namespace Conjecture.Core.Internal;

internal sealed class ConjectureData
{
    private readonly IRandom? rng;
    private readonly IReadOnlyList<IRNode>? replayNodes;
    private int cursor;
    private readonly List<IRNode> nodes = [];
    private bool frozen;

    internal Status Status { get; private set; } = Status.Valid;
    internal IReadOnlyList<IRNode> IRNodes => nodes;
    internal bool IsReplay => replayNodes is not null;

    private ConjectureData(IRandom rng) => this.rng = rng;
    private ConjectureData(IReadOnlyList<IRNode> nodes) => replayNodes = nodes;

    internal static ConjectureData ForGeneration(IRandom rng) => new(rng);
    internal static ConjectureData ForRecord(IReadOnlyList<IRNode> nodes) => new(nodes);

    internal ulong NextInteger(ulong min, ulong max)
    {
        ThrowIfNotActive();
        if (replayNodes is not null)
        {
            var node = ConsumeReplayNode();
            nodes.Add(node);
            return node.Value;
        }
        var value = PrngAdapter.NextUInt64(rng!, max - min) + min;
        nodes.Add(IRNode.ForInteger(value, min, max));
        return value;
    }

    internal bool NextBoolean()
    {
        ThrowIfNotActive();
        if (replayNodes is not null)
        {
            var node = ConsumeReplayNode();
            nodes.Add(node);
            return node.Value == 1UL;
        }
        var value = (rng!.NextUInt64() & 1UL) == 1UL;
        nodes.Add(IRNode.ForBoolean(value));
        return value;
    }

    internal ulong NextFloat64(ulong min, ulong max) => NextTyped(min, max, IRNodeKind.Float64);
    internal ulong NextFloat32(ulong min, ulong max) => NextTyped(min, max, IRNodeKind.Float32);
    internal ulong NextStringLength(ulong min, ulong max) => NextTyped(min, max, IRNodeKind.StringLength);
    internal ulong NextStringChar(ulong min, ulong max) => NextTyped(min, max, IRNodeKind.StringChar);

    private ulong NextTyped(ulong min, ulong max, IRNodeKind kind)
    {
        ThrowIfNotActive();
        if (replayNodes is not null)
        {
            var node = ConsumeReplayNode();
            nodes.Add(node);
            return node.Value;
        }
        var value = PrngAdapter.NextUInt64(rng!, max - min) + min;
        nodes.Add(kind switch
        {
            IRNodeKind.Float64 => IRNode.ForFloat64(value, min, max),
            IRNodeKind.Float32 => IRNode.ForFloat32(value, min, max),
            IRNodeKind.StringLength => IRNode.ForStringLength(value, min, max),
            IRNodeKind.StringChar => IRNode.ForStringChar(value, min, max),
            _ => IRNode.ForInteger(value, min, max),
        });
        return value;
    }

    internal byte[] NextBytes(int length)
    {
        ThrowIfNotActive();
        if (replayNodes is not null)
        {
            var node = ConsumeReplayNode();
            nodes.Add(node);
            return node.RawBytes ?? new byte[(int)node.Value];
        }
        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            rng!.NextBytes(rented.AsSpan(0, length));
            var result = rented[..length];
            nodes.Add(IRNode.ForBytes(length, result));
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    internal void MarkInvalid() => Status = Status.Invalid;
    internal void MarkInteresting() => Status = Status.Interesting;
    internal void Freeze() => frozen = true;

    private IRNode ConsumeReplayNode()
    {
        if (cursor >= replayNodes!.Count)
        {
            Status = Status.Overrun;
            throw new InvalidOperationException("Replay buffer exhausted.");
        }
        return replayNodes[cursor++];
    }

    private void ThrowIfNotActive()
    {
        if (frozen || Status != Status.Valid)
        {
            throw new InvalidOperationException("Cannot generate from a frozen or non-valid ConjectureData.");
        }
    }
}

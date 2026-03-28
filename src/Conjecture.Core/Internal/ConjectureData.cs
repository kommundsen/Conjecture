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

    private ConjectureData(IRandom rng) => this.rng = rng;
    private ConjectureData(IReadOnlyList<IRNode> nodes) => replayNodes = nodes;

    internal static ConjectureData ForGeneration(IRandom rng) => new(rng);
    internal static ConjectureData ForRecord(IReadOnlyList<IRNode> nodes) => new(nodes);

    internal ulong DrawInteger(ulong min, ulong max)
    {
        CheckCanDraw();
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

    internal bool DrawBoolean()
    {
        CheckCanDraw();
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

    internal byte[] DrawBytes(int length)
    {
        CheckCanDraw();
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

    private void CheckCanDraw()
    {
        if (frozen || Status != Status.Valid)
        {
            throw new InvalidOperationException("Cannot draw from a frozen or non-valid ConjectureData.");
        }

    }
}

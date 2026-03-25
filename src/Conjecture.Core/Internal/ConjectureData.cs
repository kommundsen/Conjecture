using System.Buffers;

namespace Conjecture.Core.Internal;

internal sealed class ConjectureData
{
    private readonly IRandom? _rng;
    private readonly IReadOnlyList<IRNode>? _replayNodes;
    private int _cursor;
    private readonly List<IRNode> _nodes = [];
    private bool _frozen;

    internal Status Status { get; private set; } = Status.Valid;
    internal IReadOnlyList<IRNode> IRNodes => _nodes;

    private ConjectureData(IRandom rng) => _rng = rng;
    private ConjectureData(IReadOnlyList<IRNode> nodes) => _replayNodes = nodes;

    internal static ConjectureData ForGeneration(IRandom rng) => new(rng);
    internal static ConjectureData ForRecord(IReadOnlyList<IRNode> nodes) => new(nodes);

    internal ulong DrawInteger(ulong min, ulong max)
    {
        CheckCanDraw();
        if (_replayNodes is not null)
        {
            var node = ConsumeReplayNode();
            _nodes.Add(node);
            return node.Value;
        }
        var value = PrngAdapter.NextUInt64(_rng!, max - min) + min;
        _nodes.Add(IRNode.ForInteger(value, min, max));
        return value;
    }

    internal bool DrawBoolean()
    {
        CheckCanDraw();
        if (_replayNodes is not null)
        {
            var node = ConsumeReplayNode();
            _nodes.Add(node);
            return node.Value == 1UL;
        }
        var value = (_rng!.NextUInt64() & 1UL) == 1UL;
        _nodes.Add(IRNode.ForBoolean(value));
        return value;
    }

    internal byte[] DrawBytes(int length)
    {
        CheckCanDraw();
        if (_replayNodes is not null)
        {
            var node = ConsumeReplayNode();
            _nodes.Add(node);
            return node.RawBytes ?? new byte[(int)node.Value];
        }
        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            _rng!.NextBytes(rented.AsSpan(0, length));
            var result = rented[..length];
            _nodes.Add(IRNode.ForBytes(length, result));
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    internal void MarkInvalid() => Status = Status.Invalid;
    internal void MarkInteresting() => Status = Status.Interesting;
    internal void Freeze() => _frozen = true;

    private IRNode ConsumeReplayNode()
    {
        if (_cursor >= _replayNodes!.Count)
        {
            Status = Status.Overrun;
            throw new InvalidOperationException("Replay buffer exhausted.");
        }
        return _replayNodes[_cursor++];
    }

    private void CheckCanDraw()
    {
        if (_frozen || Status != Status.Valid)
            throw new InvalidOperationException("Cannot draw from a frozen or non-valid ConjectureData.");
    }
}

using System.Buffers;

namespace Conjecture.Core.Internal;

internal sealed class ConjectureData
{
    private readonly IRandom _rng;
    private readonly List<IRNode> _nodes = [];
    private bool _frozen;

    internal Status Status { get; private set; } = Status.Valid;
    internal IReadOnlyList<IRNode> IRNodes => _nodes;

    private ConjectureData(IRandom rng) => _rng = rng;

    internal static ConjectureData ForGeneration(IRandom rng) => new(rng);

    internal ulong DrawInteger(ulong min, ulong max)
    {
        CheckCanDraw();
        var value = PrngAdapter.NextUInt64(_rng, max - min) + min;
        _nodes.Add(IRNode.ForInteger(value, min, max));
        return value;
    }

    internal bool DrawBoolean()
    {
        CheckCanDraw();
        var value = (_rng.NextUInt64() & 1UL) == 1UL;
        _nodes.Add(IRNode.ForBoolean(value));
        return value;
    }

    internal byte[] DrawBytes(int length)
    {
        CheckCanDraw();
        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            _rng.NextBytes(rented.AsSpan(0, length));
            var result = rented[..length];
            _nodes.Add(IRNode.ForBytes(length));
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

    private void CheckCanDraw()
    {
        if (_frozen || Status != Status.Valid)
            throw new InvalidOperationException("Cannot draw from a frozen or non-valid ConjectureData.");
    }
}

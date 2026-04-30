// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Buffers;

namespace Conjecture.Core.Internal;

internal sealed class ConjectureData
{
    private readonly IRandom? rng;
    private readonly IReadOnlyList<IRNode>? replayNodes;
    private int cursor;
    private readonly List<IRNode> nodes;
    private readonly Dictionary<string, double> observations = [];
    private bool frozen;

    internal Status Status { get; private set; } = Status.Valid;
    internal IReadOnlyList<IRNode> IRNodes => nodes;
    internal IReadOnlyDictionary<string, double> Observations => observations;
    internal bool IsReplay => replayNodes is not null;

    private ConjectureData(IRandom rng)
    {
        this.rng = rng;
        this.nodes = [];
    }

    private ConjectureData(IReadOnlyList<IRNode> nodes)
    {
        replayNodes = nodes;
        this.nodes = new(nodes.Count);
    }

    internal static ConjectureData ForGeneration(IRandom rng) => new(rng);
    internal static ConjectureData ForRecord(IReadOnlyList<IRNode> nodes) => new(nodes);
    internal static ConjectureData FromBuffer(byte[] buffer) => new(new BufferRandom(buffer));

    internal ulong NextInteger(ulong min, ulong max)
    {
        ThrowIfNotActive();
        if (replayNodes is not null)
        {
            IRNode node = ConsumeReplayNode();
            if (node.Kind != IRNodeKind.Integer || node.Value < min || node.Value > max)
            {
                Status = Status.Overrun;
                throw new InvalidOperationException("Replay node misaligned with requested Integer range.");
            }
            nodes.Add(node);
            return node.Value;
        }
        ulong value = PrngAdapter.NextUInt64(rng!, max - min) + min;
        nodes.Add(IRNode.ForInteger(value, min, max));
        return value;
    }

    internal bool NextBoolean()
    {
        ThrowIfNotActive();
        if (replayNodes is not null)
        {
            IRNode node = ConsumeReplayNode();
            if (node.Kind != IRNodeKind.Boolean)
            {
                Status = Status.Overrun;
                throw new InvalidOperationException("Replay node misaligned with requested Boolean draw.");
            }
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
            IRNode node = ConsumeReplayNode();
            if (node.Kind != kind || node.Value < min || node.Value > max)
            {
                Status = Status.Overrun;
                throw new InvalidOperationException($"Replay node misaligned with requested {kind} range.");
            }
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
            IRNode node = ConsumeReplayNode();
            if (node.Kind != IRNodeKind.Bytes || (int)node.Value != length)
            {
                Status = Status.Overrun;
                throw new InvalidOperationException("Replay node misaligned with requested Bytes draw.");
            }
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

    internal void InsertCommandStart()
    {
        ThrowIfNotActive();
        if (replayNodes is not null)
        {
            nodes.Add(ConsumeReplayNode());
        }
        else
        {
            nodes.Add(IRNode.ForCommandStart());
        }
    }

    internal void RecordObservation(string label, double value)
    {
        if (frozen)
        {
            throw new InvalidOperationException("Cannot record observations on frozen ConjectureData.");
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentException("Observation value must be finite.", nameof(value));
        }

        observations[label] = value;
    }

    internal int NodeCount => nodes.Count;

    internal void TruncateNodes(int length)
    {
        if (length < 0 || length > nodes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length,
                $"Cannot truncate to {length}; current node count is {nodes.Count}.");
        }
        nodes.RemoveRange(length, nodes.Count - length);
    }

    internal void MarkInvalid() => Status = Status.Invalid;
    internal void MarkInteresting() => Status = Status.Interesting;
    internal void Freeze() => frozen = true;

    private IRNode ConsumeReplayNode()
    {
        if (cursor >= replayNodes!.Count)
        {
            Status = Status.Overrun;
            throw new InvalidOperationException("Replay IR exhausted.");
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
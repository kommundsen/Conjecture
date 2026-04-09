// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Collections.Generic;

namespace Conjecture.Core.Internal;

internal sealed class CommandSequenceShrinkPass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        List<int> sentinels = FindSentinels(state.Nodes);
        if (sentinels.Count < 1)
        {
            return false;
        }

        if (sentinels.Count >= 2)
        {
            if (await TryTruncateFromEnd(state, sentinels))
            {
                return true;
            }

            if (await TryBinaryHalve(state, sentinels))
            {
                return true;
            }
        }

        return await TryDeleteOne(state, sentinels);
    }

    private static async ValueTask<bool> TryTruncateFromEnd(ShrinkState state, List<int> sentinels)
    {
        int lengthNodeIdx = sentinels[0] - 1;
        if (lengthNodeIdx < 0)
        {
            return false;
        }

        ulong currentLength = state.Nodes[lengthNodeIdx].Value;
        if (currentLength == 0)
        {
            return false;
        }

        IRNode[] candidate = BuildPrefixCandidate(state.Nodes, sentinels[^1], lengthNodeIdx, currentLength - 1);
        return await state.TryUpdate(candidate);
    }

    private static async ValueTask<bool> TryBinaryHalve(ShrinkState state, List<int> sentinels)
    {
        int mid = sentinels.Count / 2;
        int lengthNodeIdx = sentinels[0] - 1;
        if (lengthNodeIdx < 0)
        {
            return false;
        }

        IRNode[] candidate = BuildPrefixCandidate(state.Nodes, sentinels[mid], lengthNodeIdx, (ulong)mid);
        return await state.TryUpdate(candidate);
    }

    private static async ValueTask<bool> TryDeleteOne(ShrinkState state, List<int> sentinels)
    {
        int lengthNodeIdx = sentinels[0] - 1;
        if (lengthNodeIdx < 0)
        {
            return false;
        }

        ulong currentLength = state.Nodes[lengthNodeIdx].Value;

        for (int i = 0; i < sentinels.Count; i++)
        {
            int spanStart = sentinels[i];
            int spanEnd = i + 1 < sentinels.Count ? sentinels[i + 1] : state.Nodes.Count;
            IRNode[] withoutSpan = ShrinkHelper.WithoutInterval(state.Nodes, spanStart, spanEnd - spanStart);
            IRNode[] candidate = ShrinkHelper.Replace(withoutSpan, lengthNodeIdx, withoutSpan[lengthNodeIdx].WithValue(currentLength - 1));

            if (await state.TryUpdate(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static IRNode[] BuildPrefixCandidate(IReadOnlyList<IRNode> nodes, int count, int lengthNodeIdx, ulong newLength)
    {
        IRNode[] candidate = new IRNode[count];
        for (int i = 0; i < count; i++)
        {
            candidate[i] = i == lengthNodeIdx ? nodes[i].WithValue(newLength) : nodes[i];
        }
        return candidate;
    }

    private static List<int> FindSentinels(IReadOnlyList<IRNode> nodes)
    {
        List<int> sentinels = [];
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Kind == IRNodeKind.CommandStart)
            {
                sentinels.Add(i);
            }
        }
        return sentinels;
    }
}
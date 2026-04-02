// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Core.Internal;

internal sealed class CommandSequenceShrinkPass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        List<int> sentinels = FindSentinels(state.Nodes);
        return sentinels.Count >= 2 && await TryTruncateFromEnd(state, sentinels);
    }

    private static async ValueTask<bool> TryTruncateFromEnd(ShrinkState state, List<int> sentinels)
    {
        int lastSentinel = sentinels[^1];
        int firstSentinel = sentinels[0];
        int lengthNodeIdx = firstSentinel - 1;

        if (lengthNodeIdx < 0)
        {
            return false;
        }

        IRNode lengthNode = state.Nodes[lengthNodeIdx];
        ulong currentLength = lengthNode.Value;
        if (currentLength == 0)
        {
            return false;
        }

        // Keep everything up to (but not including) the last CommandStart, decrement the length
        IRNode[] candidate = new IRNode[lastSentinel];
        for (int i = 0; i < lastSentinel; i++)
        {
            candidate[i] = i == lengthNodeIdx
                ? lengthNode.WithValue(currentLength - 1)
                : state.Nodes[i];
        }

        return await state.TryUpdate(candidate);
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

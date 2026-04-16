// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

namespace Conjecture.Core.Internal;

internal sealed class AdaptivePass(IShrinkPass inner) : IShrinkPass
{
    private readonly HashSet<int> productiveIndices = [];

    public string PassName => inner.PassName;

    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        if (productiveIndices.Count > 0)
        {
            int[] snapshot = [.. productiveIndices];
            foreach (int i in snapshot)
            {
                if (await TryReduceAt(state, i))
                {
                    return true;
                }
                productiveIndices.Remove(i);
            }
        }

        bool progress = await inner.TryReduce(state);
        if (progress && state.LastModifiedIndex >= 0)
        {
            productiveIndices.Add(state.LastModifiedIndex);
        }
        return progress;
    }

    private static async ValueTask<bool> TryReduceAt(ShrinkState state, int index)
    {
        if (index >= state.Nodes.Count)
        {
            return false;
        }
        IRNode node = state.Nodes[index];
        if (node.Value <= node.Min)
        {
            return false;
        }
        IRNode[] candidate = [.. state.Nodes];
        candidate[index] = node.WithValue(node.Value - 1);
        return await state.TryUpdate(candidate, index);
    }
}
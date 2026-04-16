// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

namespace Conjecture.Core.Internal;

internal sealed class RedistributionPass : IShrinkPass
{
    public string PassName => "redistribution";

    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        for (int i = 0; i < state.Nodes.Count - 1; i++)
        {
            IRNode left = state.Nodes[i];
            IRNode right = state.Nodes[i + 1];

            if (left.Kind != IRNodeKind.Integer || right.Kind != IRNodeKind.Integer)
            {
                continue;
            }

            ulong maxShift = Math.Min(left.Value - left.Min, right.Max - right.Value);

            for (ulong delta = 1; delta <= maxShift; delta++)
            {
                IRNode[] candidate = [.. state.Nodes];
                candidate[i] = IRNode.ForInteger(state.Nodes[i].Value - delta, state.Nodes[i].Min, state.Nodes[i].Max);
                candidate[i + 1] = IRNode.ForInteger(state.Nodes[i + 1].Value + delta, state.Nodes[i + 1].Min, state.Nodes[i + 1].Max);
                if (await state.TryUpdate(candidate))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

namespace Conjecture.Core.Internal;

internal sealed class BlockSwappingPass : IShrinkPass
{
    public string PassName => "block_swapping";

    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        for (int i = 0; i < state.Nodes.Count - 1; i++)
        {
            IRNode a = state.Nodes[i];
            IRNode b = state.Nodes[i + 1];

            if (a.Kind != b.Kind || a.Value <= b.Value)
            {
                continue;
            }

            IRNode[] candidate = [.. state.Nodes];
            candidate[i] = state.Nodes[i + 1];
            candidate[i + 1] = state.Nodes[i];
            if (await state.TryUpdate(candidate))
            {
                return true;
            }
        }
        return false;
    }
}
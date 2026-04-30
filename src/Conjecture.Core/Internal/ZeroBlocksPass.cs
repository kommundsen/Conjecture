// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

namespace Conjecture.Core.Internal;

internal sealed class ZeroBlocksPass : IShrinkPass
{
    public string PassName => "zero_blocks";

    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        bool progress = false;
        // Right-to-left: early-drawn nodes (low indices) survive zeroing last, so they
        // remain non-minimum when later passes try to shrink the length/count node that
        // controls how many of them are consumed. This is essential for collection shrinking.
        for (int i = state.Nodes.Count - 1; i >= 0; i--)
        {
            IRNode node = state.Nodes[i];
            if (!node.IsIntegerLike)
            {
                continue;
            }

            if (node.Value == node.Min)
            {
                continue;
            }

            IRNode[] candidate = ShrinkHelper.Replace(state.Nodes, i, node.WithValue(node.Min));
            if (await state.TryUpdate(candidate))
            {
                progress = true;
            }
        }
        return progress;
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

namespace Conjecture.Core.Internal;

internal sealed class IntervalDeletionPass : IShrinkPass
{
    public string PassName => "interval_deletion";

    private static readonly int[] Sizes = [8, 4, 2];

    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        foreach (int size in Sizes)
        {
            int limit = state.Nodes.Count - size;
            for (int i = 0; i <= limit; i++)
            {
                IRNode[] candidate = ShrinkHelper.WithoutInterval(state.Nodes, i, size);
                if (await state.TryUpdate(candidate))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
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
        bool progress = false;
        for (int i = 0; i < state.Nodes.Count - 1; i++)
        {
            IRNode left = state.Nodes[i];
            IRNode right = state.Nodes[i + 1];

            if (left.Kind != IRNodeKind.Integer || right.Kind != IRNodeKind.Integer)
            {
                continue;
            }

            ulong maxShift = Math.Min(left.Value - left.Min, right.Max - right.Value);
            if (maxShift == 0)
            {
                continue;
            }

            ulong lo = 1, hi = maxShift;
            bool pairProgress = false;
            while (lo <= hi)
            {
                ulong mid = lo + ((hi - lo) / 2);
                IRNode[] candidate = ShrinkHelper.Replace(state.Nodes, i, left.WithValue(left.Value - mid));
                candidate[i + 1] = right.WithValue(right.Value + mid);
                if (await state.TryUpdate(candidate))
                {
                    pairProgress = true;
                    lo = mid + 1;
                }
                else
                {
                    if (mid == 0)
                    {
                        break;
                    }
                    hi = mid - 1;
                }
            }

            progress |= pairProgress;
        }
        return progress;
    }
}
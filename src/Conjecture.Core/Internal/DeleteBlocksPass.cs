// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal sealed class DeleteBlocksPass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        for (int i = 0; i < state.Nodes.Count; i++)
        {
            IRNode[] candidate = ShrinkHelper.Without(state.Nodes, i);
            if (await state.TryUpdate(candidate))
            {
                return true;
            }
        }
        return false;
    }
}
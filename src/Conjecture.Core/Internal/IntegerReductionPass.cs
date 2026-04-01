namespace Conjecture.Core.Internal;

internal sealed class IntegerReductionPass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        bool progress = false;
        for (int i = 0; i < state.Nodes.Count; i++)
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

            ulong lo = node.Min, hi = node.Value;
            while (lo < hi)
            {
                ulong mid = lo + (hi - lo) / 2;
                IRNode[] candidate = ShrinkHelper.Replace(state.Nodes, i, node.WithValue(mid));
                if (await state.TryUpdate(candidate))
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            if (state.Nodes[i].Value < node.Value)
            {
                progress = true;
            }
        }
        return progress;
    }
}

namespace Conjecture.Core.Internal;

internal sealed class ZeroBlocksPass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        bool progress = false;
        // Right-to-left: early-drawn nodes (low indices) survive zeroing last, so they
        // remain non-minimum when later passes try to reduce the length/count node that
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

namespace Conjecture.Core.Internal.Shrinker;

internal sealed class ZeroBlocksPass : IShrinkPass
{
    public bool TryReduce(ShrinkState state)
    {
        bool progress = false;
        // Right-to-left: early-drawn nodes (low indices) survive zeroing last, so they
        // remain non-minimum when later passes try to reduce the length/count node that
        // controls how many of them are consumed. This is essential for collection shrinking.
        for (int i = state.Nodes.Count - 1; i >= 0; i--)
        {
            var node = state.Nodes[i];
            if (!node.IsIntegerLike)
            {
                continue;
            }

            if (node.Value == node.Min)
            {
                continue;
            }

            var candidate = Replace(state.Nodes, i, node.WithValue(node.Min));
            if (state.TryUpdate(candidate))
            {
                progress = true;
            }
        }
        return progress;
    }

    private static IRNode[] Replace(IReadOnlyList<IRNode> nodes, int index, IRNode replacement)
    {
        var arr = new IRNode[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            arr[i] = i == index ? replacement : nodes[i];
        }

        return arr;
    }
}

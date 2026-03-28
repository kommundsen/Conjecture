namespace Conjecture.Core.Internal.Shrinker;

internal sealed class ZeroBlocksPass : IShrinkPass
{
    public bool TryReduce(ShrinkState state)
    {
        bool progress = false;
        for (int i = 0; i < state.Nodes.Count; i++)
        {
            var node = state.Nodes[i];
            if (node.Kind != IRNodeKind.Integer)
            {
                continue;
            }

            if (node.Value == node.Min)
            {
                continue;
            }

            var candidate = Replace(state.Nodes, i, IRNode.ForInteger(node.Min, node.Min, node.Max));
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

namespace Conjecture.Core.Internal.Shrinker;

internal sealed class RedistributionPass : IShrinkPass
{
    public bool TryReduce(ShrinkState state)
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
                IRNode[] candidate = BuildRedistributed(state.Nodes, i, delta);
                if (state.TryUpdate(candidate))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static IRNode[] BuildRedistributed(IReadOnlyList<IRNode> nodes, int i, ulong delta)
    {
        IRNode[] arr = new IRNode[nodes.Count];
        for (int j = 0; j < nodes.Count; j++)
        {
            arr[j] = nodes[j];
        }
        arr[i] = IRNode.ForInteger(nodes[i].Value - delta, nodes[i].Min, nodes[i].Max);
        arr[i + 1] = IRNode.ForInteger(nodes[i + 1].Value + delta, nodes[i + 1].Min, nodes[i + 1].Max);
        return arr;
    }
}

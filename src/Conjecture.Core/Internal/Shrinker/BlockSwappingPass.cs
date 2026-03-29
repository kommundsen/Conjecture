namespace Conjecture.Core.Internal.Shrinker;

internal sealed class BlockSwappingPass : IShrinkPass
{
    public bool TryReduce(ShrinkState state)
    {
        for (int i = 0; i < state.Nodes.Count - 1; i++)
        {
            IRNode a = state.Nodes[i];
            IRNode b = state.Nodes[i + 1];

            if (a.Kind != b.Kind || a.Value <= b.Value)
            {
                continue;
            }

            IRNode[] candidate = BuildSwapped(state.Nodes, i);
            if (state.TryUpdate(candidate))
            {
                return true;
            }
        }
        return false;
    }

    private static IRNode[] BuildSwapped(IReadOnlyList<IRNode> nodes, int i)
    {
        IRNode[] arr = new IRNode[nodes.Count];
        for (int j = 0; j < nodes.Count; j++)
        {
            arr[j] = nodes[j];
        }
        arr[i] = nodes[i + 1];
        arr[i + 1] = nodes[i];
        return arr;
    }
}

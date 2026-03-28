namespace Conjecture.Core.Internal.Shrinker;

internal sealed class DeleteBlocksPass : IShrinkPass
{
    public bool TryReduce(ShrinkState state)
    {
        for (int i = 0; i < state.Nodes.Count; i++)
        {
            var candidate = Without(state.Nodes, i);
            if (state.TryUpdate(candidate))
            {
                return true;
            }
        }
        return false;
    }

    private static IRNode[] Without(IReadOnlyList<IRNode> nodes, int index)
    {
        var arr = new IRNode[nodes.Count - 1];
        int dst = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i != index)
            {
                arr[dst++] = nodes[i];
            }
        }

        return arr;
    }
}

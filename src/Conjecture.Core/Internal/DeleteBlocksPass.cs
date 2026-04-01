namespace Conjecture.Core.Internal;

internal sealed class DeleteBlocksPass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        for (int i = 0; i < state.Nodes.Count; i++)
        {
            IRNode[] candidate = Without(state.Nodes, i);
            if (await state.TryUpdate(candidate))
            {
                return true;
            }
        }
        return false;
    }

    private static IRNode[] Without(IReadOnlyList<IRNode> nodes, int index)
    {
        IRNode[] arr = new IRNode[nodes.Count - 1];
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

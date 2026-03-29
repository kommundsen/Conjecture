namespace Conjecture.Core.Internal.Shrinker;

internal sealed class IntervalDeletionPass : IShrinkPass
{
    private static readonly int[] Sizes = [8, 4, 2];

    public bool TryReduce(ShrinkState state)
    {
        foreach (int size in Sizes)
        {
            int limit = state.Nodes.Count - size;
            for (int i = 0; i <= limit; i++)
            {
                IRNode[] candidate = WithoutInterval(state.Nodes, i, size);
                if (state.TryUpdate(candidate))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static IRNode[] WithoutInterval(IReadOnlyList<IRNode> nodes, int start, int length)
    {
        IRNode[] arr = new IRNode[nodes.Count - length];
        int dst = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i < start || i >= start + length)
            {
                arr[dst++] = nodes[i];
            }
        }
        return arr;
    }
}

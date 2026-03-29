namespace Conjecture.Core.Internal.Shrinker;

internal sealed class AdaptivePass(IShrinkPass inner) : IShrinkPass
{
    private readonly HashSet<int> productiveIndices = [];

    public bool TryReduce(ShrinkState state)
    {
        if (productiveIndices.Count > 0)
        {
            int[] snapshot = [..productiveIndices];
            foreach (int i in snapshot)
            {
                if (TryReduceAt(state, i))
                {
                    return true;
                }
                productiveIndices.Remove(i);
            }
        }

        bool progress = inner.TryReduce(state);
        if (progress && state.LastModifiedIndex >= 0)
        {
            productiveIndices.Add(state.LastModifiedIndex);
        }
        return progress;
    }

    private static bool TryReduceAt(ShrinkState state, int index)
    {
        if (index >= state.Nodes.Count)
        {
            return false;
        }
        IRNode node = state.Nodes[index];
        if (node.Value <= node.Min)
        {
            return false;
        }
        IRNode[] candidate = [..state.Nodes];
        candidate[index] = node.WithValue(node.Value - 1);
        return state.TryUpdate(candidate, index);
    }
}

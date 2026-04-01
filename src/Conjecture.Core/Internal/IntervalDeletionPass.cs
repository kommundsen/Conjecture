namespace Conjecture.Core.Internal;

internal sealed class IntervalDeletionPass : IShrinkPass
{
    private static readonly int[] Sizes = [8, 4, 2];

    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        foreach (int size in Sizes)
        {
            int limit = state.Nodes.Count - size;
            for (int i = 0; i <= limit; i++)
            {
                IRNode[] candidate = ShrinkHelper.WithoutInterval(state.Nodes, i, size);
                if (await state.TryUpdate(candidate))
                {
                    return true;
                }
            }
        }
        return false;
    }
}

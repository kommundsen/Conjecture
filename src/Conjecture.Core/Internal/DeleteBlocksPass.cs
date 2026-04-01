namespace Conjecture.Core.Internal;

internal sealed class DeleteBlocksPass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        for (int i = 0; i < state.Nodes.Count; i++)
        {
            IRNode[] candidate = ShrinkHelper.Without(state.Nodes, i);
            if (await state.TryUpdate(candidate))
            {
                return true;
            }
        }
        return false;
    }
}

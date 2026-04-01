namespace Conjecture.Core.Internal;

internal sealed class BlockSwappingPass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        for (int i = 0; i < state.Nodes.Count - 1; i++)
        {
            IRNode a = state.Nodes[i];
            IRNode b = state.Nodes[i + 1];

            if (a.Kind != b.Kind || a.Value <= b.Value)
            {
                continue;
            }

            IRNode[] candidate = ShrinkHelper.CopyNodes(state.Nodes);
            candidate[i] = state.Nodes[i + 1];
            candidate[i + 1] = state.Nodes[i];
            if (await state.TryUpdate(candidate))
            {
                return true;
            }
        }
        return false;
    }
}

namespace Conjecture.Core.Internal;

internal sealed class RedistributionPass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
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
                IRNode[] candidate = ShrinkHelper.CopyNodes(state.Nodes);
                candidate[i] = IRNode.ForInteger(state.Nodes[i].Value - delta, state.Nodes[i].Min, state.Nodes[i].Max);
                candidate[i + 1] = IRNode.ForInteger(state.Nodes[i + 1].Value + delta, state.Nodes[i + 1].Min, state.Nodes[i + 1].Max);
                if (await state.TryUpdate(candidate))
                {
                    return true;
                }
            }
        }
        return false;
    }
}

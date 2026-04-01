namespace Conjecture.Core.Internal;

internal sealed class LexMinimizePass : IShrinkPass
{
    public async ValueTask<bool> TryReduce(ShrinkState state)
    {
        bool progress = false;
        for (int i = 0; i < state.Nodes.Count; i++)
        {
            IRNode node = state.Nodes[i];
            if (!node.IsIntegerLike)
            {
                continue;
            }

            if (node.Value == node.Min)
            {
                continue;
            }

            // Try decrementing by 1; if rejected, try larger steps toward min.
            for (ulong step = 1; node.Value >= node.Min + step; step *= 2)
            {
                IRNode[] candidate = ShrinkHelper.Replace(state.Nodes, i,
                    node.WithValue(node.Value - step));
                if (await state.TryUpdate(candidate))
                {
                    progress = true;
                    break;
                }
            }
        }
        return progress;
    }
}

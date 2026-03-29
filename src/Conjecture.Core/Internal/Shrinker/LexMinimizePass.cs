namespace Conjecture.Core.Internal.Shrinker;

internal sealed class LexMinimizePass : IShrinkPass
{
    public bool TryReduce(ShrinkState state)
    {
        bool progress = false;
        for (int i = 0; i < state.Nodes.Count; i++)
        {
            var node = state.Nodes[i];
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
                var candidate = Replace(state.Nodes, i,
                    node.WithValue(node.Value - step));
                if (state.TryUpdate(candidate))
                {
                    progress = true;
                    break;
                }
            }
        }
        return progress;
    }

    private static IRNode[] Replace(IReadOnlyList<IRNode> nodes, int index, IRNode replacement)
    {
        var arr = new IRNode[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            arr[i] = i == index ? replacement : nodes[i];
        }

        return arr;
    }
}

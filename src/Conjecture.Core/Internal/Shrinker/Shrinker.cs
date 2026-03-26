namespace Conjecture.Core.Internal.Shrinker;

internal static class Shrinker
{
    private static readonly IShrinkPass[] Passes = [];

    internal static IReadOnlyList<IRNode> Shrink(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
    {
        var state = new ShrinkState(nodes, isInteresting);

        // Run registered passes to fixpoint.
        bool progress;
        do
        {
            progress = false;
            foreach (var pass in Passes)
                progress |= pass.TryReduce(state);
        } while (progress);

        // Inline integer reduction: for each integer node, try zero then binary-search toward min.
        progress = true;
        while (progress)
        {
            progress = false;
            for (int i = 0; i < state.Nodes.Count; i++)
            {
                var node = state.Nodes[i];
                if (node.Kind != IRNodeKind.Integer) continue;
                if (node.Value == node.Min) continue;

                // Try zero (or min if zero is below min).
                ulong target = node.Min;
                if (TryReplaceNode(state, i, target))
                {
                    progress = true;
                    continue;
                }

                // Binary search between min and current value.
                ulong lo = node.Min, hi = state.Nodes[i].Value;
                while (lo < hi)
                {
                    ulong mid = lo + (hi - lo) / 2;
                    if (TryReplaceNode(state, i, mid))
                    {
                        hi = state.Nodes[i].Value;
                        progress = true;
                    }
                    else
                    {
                        lo = mid + 1;
                    }
                }
            }
        }

        return state.Nodes;
    }

    private static bool TryReplaceNode(ShrinkState state, int index, ulong newValue)
    {
        var current = state.Nodes;
        var candidate = new IRNode[current.Count];
        for (int j = 0; j < current.Count; j++)
            candidate[j] = current[j];

        var old = current[index];
        candidate[index] = IRNode.ForInteger(newValue, old.Min, old.Max);
        return state.TryUpdate(candidate);
    }
}

namespace Conjecture.Core.Internal.Shrinker;

internal static class Shrinker
{
    private static readonly IShrinkPass[] Passes =
    [
        new ZeroBlocksPass(),
        new DeleteBlocksPass(),
        new LexMinimizePass(),
        new IntegerReductionPass(),
    ];

    internal static IReadOnlyList<IRNode> Shrink(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
    {
        var state = new ShrinkState(nodes, isInteresting);

        bool progress;
        do
        {
            progress = false;
            foreach (var pass in Passes)
                progress |= pass.TryReduce(state);
        } while (progress);

        return state.Nodes;
    }
}

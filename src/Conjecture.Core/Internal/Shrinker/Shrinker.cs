namespace Conjecture.Core.Internal.Shrinker;

internal static class Shrinker
{
    private static readonly IShrinkPass[][] PassTiers =
    [
        [new ZeroBlocksPass(), new DeleteBlocksPass(), new IntervalDeletionPass()],
        [new LexMinimizePass(), new IntegerReductionPass(), new BlockSwappingPass(), new RedistributionPass()],
        [new FloatSimplificationPass(), new StringAwarePass(), new AdaptivePass(new IntegerReductionPass())],
    ];

    internal static async Task<(IReadOnlyList<IRNode> Nodes, int ShrinkCount)> ShrinkAsync(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, ValueTask<Status>> isInteresting)
    {
        ShrinkState state = new(nodes, isInteresting);

        bool outerProgress;
        do
        {
            outerProgress = false;
            foreach (IShrinkPass[] tier in PassTiers)
            {
                bool tierProgress;
                do
                {
                    tierProgress = false;
                    foreach (IShrinkPass pass in tier)
                    {
                        tierProgress |= await pass.TryReduce(state);
                    }
                    outerProgress |= tierProgress;
                } while (tierProgress);
            }
        } while (outerProgress);

        return (state.Nodes, state.ShrinkCount);
    }


}

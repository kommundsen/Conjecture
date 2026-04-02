// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal static class Shrinker
{
    private static readonly IShrinkPass[][] PassTiers =
    [
        [new ZeroBlocksPass(), new DeleteBlocksPass(), new IntervalDeletionPass(), new CommandSequenceShrinkPass()],
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
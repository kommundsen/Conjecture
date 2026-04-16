// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Conjecture.Core.Internal;

internal static class Shrinker
{
    private static readonly IShrinkPass[][] PassTiers =
    [
        [new ZeroBlocksPass(), new DeleteBlocksPass(), new IntervalDeletionPass(), new CommandSequenceShrinkPass()],
        [new LexMinimizePass(), new IntegerReductionPass(), new BlockSwappingPass(), new RedistributionPass()],
        [new FloatSimplificationPass(), new StringAwarePass(), new NumericAwareShrinkPass(), new AdaptivePass(new IntegerReductionPass())],
    ];

    internal static Task<(IReadOnlyList<IRNode> Nodes, int ShrinkCount)> ShrinkAsync(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, ValueTask<Status>> isInteresting)
    {
        return ShrinkAsync(nodes, isInteresting, NullLogger.Instance);
    }

    internal static async Task<(IReadOnlyList<IRNode> Nodes, int ShrinkCount)> ShrinkAsync(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, ValueTask<Status>> isInteresting,
        ILogger logger)
    {
        ShrinkState state = new(nodes, isInteresting);
        Log.ShrinkingStarted(logger, nodes.Count);
        Stopwatch sw = Stopwatch.StartNew();

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
                        bool madeProgress = await pass.TryReduce(state);
                        tierProgress |= madeProgress;
                        Instruments.ShrinkPassesTotal.Add(1, new KeyValuePair<string, object?>("pass_name", pass.PassName));
                        Log.ShrinkPassProgress(logger, pass.PassName, madeProgress);
                    }
                    outerProgress |= tierProgress;
                } while (tierProgress);
            }
        } while (outerProgress);

        sw.Stop();
        Instruments.ShrinkReductionsTotal.Add(state.ShrinkCount);
        Log.ShrinkingCompleted(logger, state.Nodes.Count, state.ShrinkCount, sw.Elapsed.TotalMilliseconds);
        return (state.Nodes, state.ShrinkCount);
    }


}
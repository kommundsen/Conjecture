// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal.Shrinker;

public class ShrinkerTests
{
    // Helper: build a node list with a single integer node of the given value.
    private static IReadOnlyList<IRNode> SingleIntegerNodes(ulong value, ulong min = 0, ulong max = ulong.MaxValue)
        => [IRNode.ForInteger(value, min, max)];

    // Predicate that is "interesting" when the integer value is >= threshold.
    private static Func<IReadOnlyList<IRNode>, Status> InterestingWhenAtLeast(ulong threshold) =>
        nodes =>
        {
            ConjectureData data = ConjectureData.ForRecord(nodes);
            try
            {
                ulong v = data.NextInteger(0, ulong.MaxValue);
                return v >= threshold ? Status.Interesting : Status.Valid;
            }
            catch
            {
                return Status.Overrun;
            }
        };

    [Fact]
    public async Task Shrink_AlreadyMinimalBuffer_ReturnsUnchanged()
    {
        // A buffer whose single integer node is already at the minimum (0)
        // is already as small as it can get; shrinking should leave it unchanged.
        IReadOnlyList<IRNode> nodes = SingleIntegerNodes(0, 0, 100);
        // Predicate requires a draw — empty replay overruns and is not interesting,
        // so DeleteBlocksPass cannot remove the node.
        static Status IsInteresting(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try { data.NextInteger(0, 100); return Status.Interesting; }
            catch { return Status.Overrun; }
        }

        (IReadOnlyList<IRNode> result, int _) = await Core.Internal.Shrinker.ShrinkAsync(
            nodes, n => new ValueTask<Status>(IsInteresting(n)));

        IRNode single = Assert.Single(result);
        Assert.Equal(0UL, single.Value);
    }

    [Fact]
    public async Task Shrink_ReducibleInteger_ProducesLexicographicallySmaller()
    {
        // Buffer with a large integer; shrinking should reduce the value toward the threshold.
        IReadOnlyList<IRNode> nodes = SingleIntegerNodes(1000, 0, 2000);
        // Interesting when value >= 5.
        Func<IReadOnlyList<IRNode>, Status> isInteresting = InterestingWhenAtLeast(5);

        (IReadOnlyList<IRNode> result, int _) = await Core.Internal.Shrinker.ShrinkAsync(
            nodes, n => new ValueTask<Status>(isInteresting(n)));

        Assert.Single(result);
        Assert.True(result[0].Value < 1000, $"Expected value < 1000, got {result[0].Value}");
    }

    [Fact]
    public async Task Shrink_PreservesFailure_ResultIsStillInteresting()
    {
        // Whatever the shrinker produces, replaying through the predicate must
        // still yield Interesting — shrinking must never discard the failure.
        IReadOnlyList<IRNode> nodes = SingleIntegerNodes(500, 0, 1000);
        Func<IReadOnlyList<IRNode>, Status> isInteresting = InterestingWhenAtLeast(10);

        (IReadOnlyList<IRNode> result, int _) = await Core.Internal.Shrinker.ShrinkAsync(
            nodes, n => new ValueTask<Status>(isInteresting(n)));

        Status status = isInteresting(result);
        Assert.Equal(Status.Interesting, status);
    }

    [Fact]
    public async Task Shrink_WhenNoPassMakesProgress_ReturnsCurrentBest()
    {
        // If the only interesting buffer is the one already at minimum expressible
        // value for the predicate, no pass can make progress and the shrinker
        // must terminate and return that buffer.
        IReadOnlyList<IRNode> nodes = SingleIntegerNodes(1, 0, 100);
        // Interesting only when value == 1 (exactly at threshold, 0 would not be interesting).
        static Status IsInteresting(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try
            {
                ulong v = data.NextInteger(0, 100);
                return v == 1 ? Status.Interesting : Status.Valid;
            }
            catch
            {
                return Status.Overrun;
            }
        }

        (IReadOnlyList<IRNode> result, int _) = await Core.Internal.Shrinker.ShrinkAsync(
            nodes, n => new ValueTask<Status>(IsInteresting(n)));

        Assert.Single(result);
        Assert.Equal(1UL, result[0].Value);
    }

    [Fact]
    public async Task Shrink_MultipleNodes_ShrinksToMinimalInterestingCombination()
    {
        // Two integer nodes; interesting when their sum >= 10.
        // Starting from [50, 50], the shrinker should reduce dramatically.
        IRNode[] nodes =
        [
            IRNode.ForInteger(50, 0, 100),
            IRNode.ForInteger(50, 0, 100),
        ];

        static Status IsInteresting(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try
            {
                ulong a = data.NextInteger(0, 100);
                ulong b = data.NextInteger(0, 100);
                return a + b >= 10 ? Status.Interesting : Status.Valid;
            }
            catch
            {
                return Status.Overrun;
            }
        }

        (IReadOnlyList<IRNode> result, int _) = await Core.Internal.Shrinker.ShrinkAsync(
            nodes, n => new ValueTask<Status>(IsInteresting(n)));

        Assert.Equal(2, result.Count);
        ulong sum = result[0].Value + result[1].Value;
        Assert.True(sum < 100, $"Expected sum < 100, got {sum}");
        Assert.Equal(Status.Interesting, IsInteresting(result));
    }
}
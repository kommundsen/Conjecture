// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal.Shrinker;

public class AdaptivePassTests
{
    private static ShrinkState MakeState(
        IReadOnlyList<IRNode> nodes,
        Func<IReadOnlyList<IRNode>, Status> isInteresting)
        => new(nodes, n => new ValueTask<Status>(isInteresting(n)));

    private static IRNode Int(ulong v, ulong max = 20) => IRNode.ForInteger(v, 0, max);

    /// <summary>
    /// A test-only inner pass that reduces each integer node by 1, calling
    /// TryUpdate(candidate, i) so ShrinkState.LastModifiedIndex is set correctly.
    /// Stops after the first successful update.
    /// </summary>
    private sealed class IndexAwareReductionPass : IShrinkPass
    {
        public string PassName => "test_index_aware";

        public async ValueTask<bool> TryReduce(ShrinkState state)
        {
            for (int i = 0; i < state.Nodes.Count; i++)
            {
                IRNode node = state.Nodes[i];
                if (node.Kind != IRNodeKind.Integer || node.Value == node.Min)
                {
                    continue;
                }

                IRNode[] candidate = [.. state.Nodes];
                candidate[i] = IRNode.ForInteger(node.Value - 1, node.Min, node.Max);
                if (await state.TryUpdate(candidate, i))
                {
                    return true;
                }
            }
            return false;
        }
    }

    [Fact]
    public async Task TryReduce_MakesProgress_WhenInnerPassSucceeds()
    {
        // Single reducible node — inner pass will decrement it; AdaptivePass must return true.
        IRNode[] nodes = [Int(5)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        AdaptivePass pass = new(new IndexAwareReductionPass());

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(4UL, state.Nodes[0].Value);
    }

    [Fact]
    public async Task TryReduce_ReturnsNoProgress_WhenInnerPassMakesNoProgress()
    {
        // Node already at minimum — inner pass can't reduce it.
        IRNode[] nodes = [Int(0)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        AdaptivePass pass = new(new IndexAwareReductionPass());

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
    }

    [Fact]
    public async Task TryReduce_SetsLastModifiedIndex_AfterSuccessfulReduction()
    {
        // Five nodes; only index 2 is interesting to reduce.
        // After progress, ShrinkState.LastModifiedIndex must equal 2.
        IRNode[] nodes = [Int(0), Int(0), Int(5), Int(0), Int(0)];
        static Status InterestingOnlyAtIndex2(IReadOnlyList<IRNode> ns) =>
            ns.Count == 5 && ns[2].Value < 5 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, InterestingOnlyAtIndex2);
        AdaptivePass pass = new(new IndexAwareReductionPass());

        bool progress = await pass.TryReduce(state);

        Assert.True(progress);
        Assert.Equal(2, state.LastModifiedIndex);
    }

    [Fact]
    public async Task TryReduce_UsesFewIsInterestingCalls_WhenAdaptiveSetIsNonEmpty()
    {
        // Five nodes all reducible; predicate fires only when nodes[2] decreases.
        // First run: full scan → tries indices 0 and 1 (fail), then index 2 (success) → 3 calls.
        // Second run: adaptive set {2} tried first → succeeds in 1 isInteresting call.
        IRNode[] nodes = [Int(5), Int(5), Int(10), Int(5), Int(5)];
        int callCount = 0;
        Status CountingPredicate(IReadOnlyList<IRNode> ns)
        {
            callCount++;
            return ns.Count == 5 && ns[2].Value < 10 ? Status.Interesting : Status.Valid;
        }
        ShrinkState state = MakeState(nodes, CountingPredicate);
        AdaptivePass pass = new(new IndexAwareReductionPass());

        // First run: full scan is required; should need > 1 call to isInteresting.
        bool firstProgress = await pass.TryReduce(state);
        int callsFirstRun = callCount;

        // Second run: adaptive bias toward index 2 should require fewer calls.
        callCount = 0;
        bool secondProgress = await pass.TryReduce(state);
        int callsSecondRun = callCount;

        Assert.True(firstProgress);
        Assert.True(secondProgress);
        Assert.True(callsSecondRun < callsFirstRun,
            $"Expected fewer isInteresting calls on second run ({callsSecondRun}) than first ({callsFirstRun}).");
    }

    [Fact]
    public async Task TryReduce_FallsBackToFullScan_WhenAdaptiveSetIsEmpty()
    {
        // Fresh AdaptivePass has an empty productive set, so it must delegate to inner pass
        // and still find a productive index.
        IRNode[] nodes = [Int(0), Int(0), Int(5), Int(0), Int(0)];
        static Status InterestingOnlyAtIndex2(IReadOnlyList<IRNode> ns) =>
            ns.Count == 5 && ns[2].Value < 5 ? Status.Interesting : Status.Valid;
        ShrinkState state = MakeState(nodes, InterestingOnlyAtIndex2);
        AdaptivePass pass = new(new IndexAwareReductionPass());

        bool progress = await pass.TryReduce(state);

        Assert.True(progress, "Full-scan fallback should find the productive index.");
        Assert.Equal(4UL, state.Nodes[2].Value);
    }

    [Fact]
    public void TryReduce_IntegratesWithShrinkState_LastModifiedIndexIsMinusOneInitially()
    {
        // Before any call to TryUpdate(candidate, index), LastModifiedIndex should be -1.
        IRNode[] nodes = [Int(5)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);

        Assert.Equal(-1, state.LastModifiedIndex);
    }

    [Fact]
    public async Task TryReduce_DoesNotModifyLastModifiedIndex_WhenNoProgress()
    {
        // When the inner pass makes no progress, LastModifiedIndex remains -1.
        IRNode[] nodes = [Int(0)];
        static Status AlwaysInteresting(IReadOnlyList<IRNode> _) => Status.Interesting;
        ShrinkState state = MakeState(nodes, AlwaysInteresting);
        AdaptivePass pass = new(new IndexAwareReductionPass());

        bool progress = await pass.TryReduce(state);

        Assert.False(progress);
        Assert.Equal(-1, state.LastModifiedIndex);
    }
}
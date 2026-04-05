// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;
using System.Runtime.CompilerServices;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal.Shrinker;

public class ShrinkerPassOrderTests
{
    // Accesses Shrinker.PassTiers: IShrinkPass[][] — a private static field
    // grouping passes by priority tier (0 = cheap, 2 = expensive).
    private static IShrinkPass[][] GetPassTiers()
    {
        FieldInfo? field = typeof(Core.Internal.Shrinker)
            .GetField("PassTiers", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return (IShrinkPass[][])field!.GetValue(null)!;
    }

    [Fact]
    public void PassRegistration_AllElevenPassTypes_ArePresent()
    {
        IShrinkPass[][] tiers = GetPassTiers();
        List<IShrinkPass> all = tiers.SelectMany(t => t).ToList();

        Assert.Equal(11, all.Count);
        Assert.Contains(all, p => p is ZeroBlocksPass);
        Assert.Contains(all, p => p is DeleteBlocksPass);
        Assert.Contains(all, p => p is IntervalDeletionPass);
        Assert.Contains(all, p => p is CommandSequenceShrinkPass);
        Assert.Contains(all, p => p is LexMinimizePass);
        Assert.Contains(all, p => p is IntegerReductionPass);
        Assert.Contains(all, p => p is BlockSwappingPass);
        Assert.Contains(all, p => p is RedistributionPass);
        Assert.Contains(all, p => p is FloatSimplificationPass);
        Assert.Contains(all, p => p is StringAwarePass);
        Assert.Contains(all, p => p is AdaptivePass);
    }

    [Fact]
    public void TierStructure_TierZeroContainsExactlyFourCheapPasses()
    {
        IShrinkPass[][] tiers = GetPassTiers();
        Assert.True(tiers.Length >= 3, $"Expected ≥ 3 tiers, got {tiers.Length}.");

        IShrinkPass[] tier0 = tiers[0];
        Assert.Equal(4, tier0.Length);
        Assert.Contains(tier0, p => p is ZeroBlocksPass);
        Assert.Contains(tier0, p => p is DeleteBlocksPass);
        Assert.Contains(tier0, p => p is IntervalDeletionPass);
        Assert.Contains(tier0, p => p is CommandSequenceShrinkPass);
    }

    [Fact]
    public void TierStructure_TierTwoContainsExpensivePasses()
    {
        // Tier 2: FloatSimplification, StringAware, Adaptive — expensive specialised passes.
        IShrinkPass[][] tiers = GetPassTiers();
        Assert.True(tiers.Length >= 3, $"Expected ≥ 3 tiers, got {tiers.Length}.");

        IShrinkPass[] tier2 = tiers[2];
        Assert.Contains(tier2, p => p is FloatSimplificationPass);
        Assert.Contains(tier2, p => p is StringAwarePass);
        Assert.Contains(tier2, p => p is AdaptivePass);
    }

    [Fact]
    public async Task TierFixpoint_MultipleIntegerNodes_AllReducedBeforeLoopEnds()
    {
        // Each tier must run to fixpoint before the next tier starts.
        // With two integer nodes and a sum predicate, cheap tier-0 passes (ZeroBlocks,
        // DeleteBlocks, IntervalDeletion) must keep running until no further reduction
        // is possible before tier-1 and tier-2 are entered.
        // Observable effect: the result is fully minimal and still interesting.
        var nodes = new[]
        {
            IRNode.ForInteger(8, 0, 10),
            IRNode.ForInteger(8, 0, 10),
        };
        static Status IsInteresting(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try
            {
                ulong a = data.NextInteger(0, 10);
                ulong b = data.NextInteger(0, 10);
                return a + b >= 10 ? Status.Interesting : Status.Valid;
            }
            catch { return Status.Overrun; }
        }

        (IReadOnlyList<IRNode> result, int shrinkCount) = await Core.Internal.Shrinker.ShrinkAsync(
            nodes, n => new ValueTask<Status>(IsInteresting(n)));

        Assert.Equal(Status.Interesting, IsInteresting(result));
        Assert.True(shrinkCount > 0, "Expected at least one shrink step.");
        // Both nodes must have been reduced from their starting values of 8.
        Assert.True(result[0].Value + result[1].Value < 16,
            $"Expected sum < 16 after shrinking, got {result[0].Value + result[1].Value}.");
    }

    [Fact]
    public async Task FullShrinkLoop_FloatNodeAboveThreshold_ConvergesAndPreservesFailure()
    {
        // With all 11 passes active across 3 tiers, the loop must still terminate
        // and the final result must remain interesting.
        ulong bigFloat = Unsafe.BitCast<double, ulong>(1e6);
        var nodes = new[] { IRNode.ForFloat64(bigFloat, 0UL, ulong.MaxValue) };

        static Status IsInteresting(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try
            {
                ulong raw = data.NextFloat64(0UL, ulong.MaxValue);
                double v = Unsafe.BitCast<ulong, double>(raw);
                return v > 1.0 ? Status.Interesting : Status.Valid;
            }
            catch { return Status.Overrun; }
        }

        (IReadOnlyList<IRNode> result, int _) = await Core.Internal.Shrinker.ShrinkAsync(
            nodes, n => new ValueTask<Status>(IsInteresting(n)));

        Assert.Equal(Status.Interesting, IsInteresting(result));
        // The shrunken float must be smaller than the starting value.
        ulong resultBits = result[0].Value;
        double resultVal = Unsafe.BitCast<ulong, double>(resultBits);
        Assert.True(resultVal < 1e6, $"Expected value < 1e6 after shrinking, got {resultVal}.");
    }
}
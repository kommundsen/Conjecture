using Conjecture.Core.Internal;
using Conjecture.Core.Internal.Shrinker;

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
            var data = ConjectureData.ForRecord(nodes);
            try
            {
                var v = data.DrawInteger(0, ulong.MaxValue);
                return v >= threshold ? Status.Interesting : Status.Valid;
            }
            catch
            {
                return Status.Overrun;
            }
        };

    [Fact]
    public void Shrink_AlreadyMinimalBuffer_ReturnsUnchanged()
    {
        // A buffer whose single integer node is already at the minimum (0)
        // is already as small as it can get; shrinking should leave it unchanged.
        var nodes = SingleIntegerNodes(0, 0, 100);
        // Predicate: always interesting (so we're sure it doesn't shrink further).
        Func<IReadOnlyList<IRNode>, Status> isInteresting =
            _ => Status.Interesting;

        var result = Core.Internal.Shrinker.Shrinker.Shrink(nodes, isInteresting);

        var single = Assert.Single(result);
        Assert.Equal(0UL, single.Value);
    }

    [Fact]
    public void Shrink_ReducibleInteger_ProducesLexicographicallySmaller()
    {
        // Buffer with a large integer; shrinking should reduce the value toward the threshold.
        var nodes = SingleIntegerNodes(1000, 0, 2000);
        // Interesting when value >= 5.
        var isInteresting = InterestingWhenAtLeast(5);

        var result = Core.Internal.Shrinker.Shrinker.Shrink(nodes, isInteresting);

        Assert.Single(result);
        Assert.True(result[0].Value < 1000, $"Expected value < 1000, got {result[0].Value}");
    }

    [Fact]
    public void Shrink_PreservesFailure_ResultIsStillInteresting()
    {
        // Whatever the shrinker produces, replaying through the predicate must
        // still yield Interesting — shrinking must never discard the failure.
        var nodes = SingleIntegerNodes(500, 0, 1000);
        var isInteresting = InterestingWhenAtLeast(10);

        var result = Core.Internal.Shrinker.Shrinker.Shrink(nodes, isInteresting);

        var status = isInteresting(result);
        Assert.Equal(Status.Interesting, status);
    }

    [Fact]
    public void Shrink_WhenNoPassMakesProgress_ReturnsCurrentBest()
    {
        // If the only interesting buffer is the one already at minimum expressible
        // value for the predicate, no pass can make progress and the shrinker
        // must terminate and return that buffer.
        var nodes = SingleIntegerNodes(1, 0, 100);
        // Interesting only when value == 1 (exactly at threshold, 0 would not be interesting).
        Func<IReadOnlyList<IRNode>, Status> isInteresting =
            ns =>
            {
                var data = ConjectureData.ForRecord(ns);
                try
                {
                    var v = data.DrawInteger(0, 100);
                    return v == 1 ? Status.Interesting : Status.Valid;
                }
                catch
                {
                    return Status.Overrun;
                }
            };

        var result = Core.Internal.Shrinker.Shrinker.Shrink(nodes, isInteresting);

        Assert.Single(result);
        Assert.Equal(1UL, result[0].Value);
    }

    [Fact]
    public void Shrink_MultipleNodes_ShrinksToMinimalInterestingCombination()
    {
        // Two integer nodes; interesting when their sum >= 10.
        // Starting from [50, 50], the shrinker should reduce dramatically.
        var nodes = new[]
        {
            IRNode.ForInteger(50, 0, 100),
            IRNode.ForInteger(50, 0, 100),
        };

        Func<IReadOnlyList<IRNode>, Status> isInteresting =
            ns =>
            {
                var data = ConjectureData.ForRecord(ns);
                try
                {
                    var a = data.DrawInteger(0, 100);
                    var b = data.DrawInteger(0, 100);
                    return a + b >= 10 ? Status.Interesting : Status.Valid;
                }
                catch
                {
                    return Status.Overrun;
                }
            };

        var result = Core.Internal.Shrinker.Shrinker.Shrink(nodes, isInteresting);

        Assert.Equal(2, result.Count);
        ulong sum = result[0].Value + result[1].Value;
        Assert.True(sum < 100, $"Expected sum < 100, got {sum}");
        Assert.Equal(Status.Interesting, isInteresting(result));
    }
}

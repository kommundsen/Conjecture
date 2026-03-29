using System.Runtime.CompilerServices;

using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal.Shrinker;

public class ShrinkQualityAdvancedTests
{
    // ── Float > threshold shrinks to just above threshold ─────────────────────

    [Fact]
    public void Shrink_FloatAboveOne_ShrinksToJustAboveOne()
    {
        ulong largeBits = Unsafe.BitCast<double, ulong>(1e200);
        IRNode[] nodes = [IRNode.ForFloat64(largeBits, 0UL, ulong.MaxValue)];

        static Status FloatAboveOne(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try
            {
                ulong raw = data.DrawFloat64(0UL, ulong.MaxValue);
                double v = Unsafe.BitCast<ulong, double>(raw);
                return v > 1.0 ? Status.Interesting : Status.Valid;
            }
            catch
            {
                return Status.Overrun;
            }
        }

        (IReadOnlyList<IRNode> result, _) = Core.Internal.Shrinker.Shrinker.Shrink(nodes, FloatAboveOne);

        ulong resultBits = result[0].Value;
        double resultValue = Unsafe.BitCast<ulong, double>(resultBits);

        Assert.Equal(Status.Interesting, FloatAboveOne(result));
        Assert.False(double.IsNaN(resultValue), "Shrunken float must not be NaN.");
        Assert.False(double.IsInfinity(resultValue), "Shrunken float must not be infinite.");
        Assert.True(resultValue < 2.0,
            $"Expected shrunken float < 2.0 (just above threshold 1.0), got {resultValue}.");
    }

    // ── String with "error" prefix shrinks to exactly "error" ─────────────────

    [Fact]
    public void Shrink_StringWithErrorPrefix_ShrinksToExactlyError()
    {
        IRNode[] nodes =
        [
            IRNode.ForStringLength(8, 0, 20),
            IRNode.ForStringChar('e', 32, 126),
            IRNode.ForStringChar('r', 32, 126),
            IRNode.ForStringChar('r', 32, 126),
            IRNode.ForStringChar('o', 32, 126),
            IRNode.ForStringChar('r', 32, 126),
            IRNode.ForStringChar('X', 32, 126),
            IRNode.ForStringChar('X', 32, 126),
            IRNode.ForStringChar('X', 32, 126),
        ];

        static Status ContainsError(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try
            {
                int length = (int)data.DrawStringLength(0, 20);
                char[] chars = new char[length];
                for (int i = 0; i < length; i++)
                {
                    chars[i] = (char)data.DrawStringChar(32, 126);
                }
                return new string(chars).Contains("error") ? Status.Interesting : Status.Valid;
            }
            catch
            {
                return Status.Overrun;
            }
        }

        (IReadOnlyList<IRNode> result, _) = Core.Internal.Shrinker.Shrinker.Shrink(nodes, ContainsError);

        ConjectureData replay = ConjectureData.ForRecord(result);
        int shrunkenLength = (int)replay.DrawStringLength(0, 20);
        char[] shrunkenChars = new char[shrunkenLength];
        for (int i = 0; i < shrunkenLength; i++)
        {
            shrunkenChars[i] = (char)replay.DrawStringChar(32, 126);
        }
        string shrunken = new(shrunkenChars);

        Assert.Equal(Status.Interesting, ContainsError(result));
        Assert.Equal("error", shrunken);
    }

    // ── List sum > 100 shrinks to single element 101 ──────────────────────────

    [Fact]
    public void Shrink_ListSumAbove100_ShrinksToSingleElement101()
    {
        IRNode[] nodes =
        [
            IRNode.ForInteger(3, 0, 10),    // list size = 3
            IRNode.ForInteger(200, 0, 200), // element 0
            IRNode.ForInteger(200, 0, 200), // element 1
            IRNode.ForInteger(200, 0, 200), // element 2
        ];

        static Status SumOver100(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try
            {
                int size = (int)data.DrawInteger(0, 10);
                ulong sum = 0;
                for (int i = 0; i < size; i++)
                {
                    sum += data.DrawInteger(0, 200);
                }
                return sum > 100 ? Status.Interesting : Status.Valid;
            }
            catch
            {
                return Status.Overrun;
            }
        }

        (IReadOnlyList<IRNode> result, _) = Core.Internal.Shrinker.Shrinker.Shrink(nodes, SumOver100);

        ConjectureData replay = ConjectureData.ForRecord(result);
        int shrunkenSize = (int)replay.DrawInteger(0, 10);
        ulong shrunkenSum = 0;
        for (int i = 0; i < shrunkenSize; i++)
        {
            shrunkenSum += replay.DrawInteger(0, 200);
        }

        Assert.Equal(Status.Interesting, SumOver100(result));
        Assert.Equal(1, shrunkenSize);
        Assert.Equal(101UL, shrunkenSum);
    }

    // ── Adjacent integers a > b shrinks to minimal pair (1, 0) ───────────────

    [Fact]
    public void Shrink_AdjacentIntegers_AGreaterThanB_ShrinksToMinimalPair()
    {
        // a=100, b=5 satisfies a > b. Minimal satisfying pair is (1, 0).
        IRNode[] nodes =
        [
            IRNode.ForInteger(100, 0, 200),
            IRNode.ForInteger(5, 0, 200),
        ];

        static Status AGreaterThanB(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try
            {
                ulong a = data.DrawInteger(0, 200);
                ulong b = data.DrawInteger(0, 200);
                return a > b ? Status.Interesting : Status.Valid;
            }
            catch
            {
                return Status.Overrun;
            }
        }

        (IReadOnlyList<IRNode> result, _) = Core.Internal.Shrinker.Shrinker.Shrink(nodes, AGreaterThanB);

        Assert.Equal(Status.Interesting, AGreaterThanB(result));
        Assert.Equal(2, result.Count);
        Assert.Equal(1UL, result[0].Value);
        Assert.Equal(0UL, result[1].Value);
    }

    // ── Nested list shrinks both outer and inner dimensions ───────────────────

    [Fact]
    public void Shrink_NestedList_ShrinksOuterAndInnerDimensions()
    {
        IRNode[] nodes =
        [
            IRNode.ForInteger(2, 0, 5),   // outer size = 2
            IRNode.ForInteger(2, 0, 5),   // inner[0] size = 2
            IRNode.ForInteger(10, 0, 10), // inner[0][0]
            IRNode.ForInteger(10, 0, 10), // inner[0][1]
            IRNode.ForInteger(2, 0, 5),   // inner[1] size = 2
            IRNode.ForInteger(10, 0, 10), // inner[1][0]
            IRNode.ForInteger(10, 0, 10), // inner[1][1]
        ];

        static Status InnerSumOver3(IReadOnlyList<IRNode> ns)
        {
            ConjectureData data = ConjectureData.ForRecord(ns);
            try
            {
                int outerSize = (int)data.DrawInteger(0, 5);
                for (int i = 0; i < outerSize; i++)
                {
                    int innerSize = (int)data.DrawInteger(0, 5);
                    ulong innerSum = 0;
                    for (int j = 0; j < innerSize; j++)
                    {
                        innerSum += data.DrawInteger(0, 10);
                    }
                    if (innerSum > 3)
                    {
                        return Status.Interesting;
                    }
                }
                return Status.Valid;
            }
            catch
            {
                return Status.Overrun;
            }
        }

        (IReadOnlyList<IRNode> result, _) = Core.Internal.Shrinker.Shrinker.Shrink(nodes, InnerSumOver3);

        ConjectureData replay = ConjectureData.ForRecord(result);
        int outerSize = (int)replay.DrawInteger(0, 5);
        int firstInnerSize = (int)replay.DrawInteger(0, 5);
        ulong firstInnerSum = 0;
        for (int i = 0; i < firstInnerSize; i++)
        {
            firstInnerSum += replay.DrawInteger(0, 10);
        }

        Assert.Equal(Status.Interesting, InnerSumOver3(result));
        Assert.Equal(1, outerSize);
        Assert.Equal(1, firstInnerSize);
        Assert.Equal(4UL, firstInnerSum);
    }
}
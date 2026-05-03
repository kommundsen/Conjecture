// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class RangeStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void Ranges_GeneratesWellFormedRangesAgainstMaxValueArray()
    {
        Strategy<Range> strategy = Strategy.Ranges();
        ConjectureData data = MakeData();
        int maxValue = 100;

        for (int i = 0; i < 500; i++)
        {
            Range range = strategy.Generate(data);
            (int offset, int length) = range.GetOffsetAndLength(maxValue);
            Assert.True(length >= 0, $"Expected Length >= 0 but got {length} for range {range}");
        }
    }

    [Fact]
    public void Ranges_StartAndEndStayWithinMaxValue()
    {
        Strategy<Range> strategy = Strategy.Ranges();
        ConjectureData data = MakeData();
        int maxValue = 100;

        for (int i = 0; i < 500; i++)
        {
            Range range = strategy.Generate(data);
            Assert.True(range.Start.Value >= 0 && range.Start.Value <= maxValue,
                $"Start.Value {range.Start.Value} out of [0, {maxValue}]");
            Assert.True(range.End.Value >= 0 && range.End.Value <= maxValue,
                $"End.Value {range.End.Value} out of [0, {maxValue}]");
        }
    }

    [Fact]
    public void Ranges_MixedFromStartFromEndReachable()
    {
        Strategy<Range> strategy = Strategy.Ranges();
        ConjectureData data = MakeData();
        bool seenMixed = false;
        bool seenBothFromStart = false;
        bool seenBothFromEnd = false;

        for (int i = 0; i < 1000; i++)
        {
            Range range = strategy.Generate(data);
            bool startFromEnd = range.Start.IsFromEnd;
            bool endFromEnd = range.End.IsFromEnd;

            if (startFromEnd != endFromEnd)
            {
                seenMixed = true;
            }
            else if (!startFromEnd && !endFromEnd)
            {
                seenBothFromStart = true;
            }
            else if (startFromEnd && endFromEnd)
            {
                seenBothFromEnd = true;
            }

            if (seenMixed && seenBothFromStart && seenBothFromEnd)
            {
                break;
            }
        }

        Assert.True(seenMixed, "No mixed from-start/from-end range generated in 1000 draws");
        Assert.True(seenBothFromStart, "No both-from-start range generated in 1000 draws");
        Assert.True(seenBothFromEnd, "No both-from-end range generated in 1000 draws");
    }

    [Fact]
    public async Task Ranges_ShrinksTowardZeroToZero()
    {
        Strategy<Range> strategy = Strategy.Ranges();
        ConjectureSettings settings = new() { MaxExamples = 200, Seed = 1UL };

        TestRunResult result = await TestRunner.Run(settings, data =>
        {
            Range range = strategy.Generate(data);
            throw new Exception($"always fails: {range}");
        });

        Assert.False(result.Passed);
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        Range shrunk = strategy.Generate(replay);
        Assert.Equal(0, shrunk.Start.Value);
        Assert.False(shrunk.Start.IsFromEnd);
        Assert.Equal(0, shrunk.End.Value);
        Assert.False(shrunk.End.IsFromEnd);
    }

    [Fact]
    public void Ranges_RespectsCustomMaxValue()
    {
        Strategy<Range> strategy = Strategy.Ranges(10);
        ConjectureData data = MakeData();

        for (int i = 0; i < 200; i++)
        {
            Range range = strategy.Generate(data);
            Assert.True(range.Start.Value <= 10, $"Start.Value {range.Start.Value} exceeds maxValue 10");
            Assert.True(range.End.Value <= 10, $"End.Value {range.End.Value} exceeds maxValue 10");
        }
    }

    [Fact]
    public void Ranges_ThrowsOnNegativeMaxValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Strategy.Ranges(-1));
    }
}

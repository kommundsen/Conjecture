// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class TimeSpanStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void TimeSpans_BoundedRange_ReturnsInRange()
    {
        TimeSpan min = TimeSpan.FromSeconds(-3600);
        TimeSpan max = TimeSpan.FromSeconds(3600);
        Strategy<TimeSpan> strategy = Strategy.TimeSpans(min, max);
        ConjectureData data = MakeData();

        for (int i = 0; i < 1000; i++)
        {
            TimeSpan value = strategy.Generate(data);
            Assert.InRange(value, min, max);
        }
    }

    [Fact]
    public void TimeSpans_DefaultRange_DoesNotThrow()
    {
        Strategy<TimeSpan> strategy = Strategy.TimeSpans();
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            _ = strategy.Generate(data);
        }
    }

    [Fact]
    public void TimeSpans_MinEqualsMax_ReturnsConstant()
    {
        TimeSpan t = TimeSpan.FromMinutes(90);
        Strategy<TimeSpan> strategy = Strategy.TimeSpans(t, t);
        ConjectureData data = MakeData();

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(t, strategy.Generate(data));
        }
    }

    [Fact]
    public void TimeSpans_MinGreaterThanMax_Throws()
    {
        TimeSpan big = TimeSpan.FromHours(10);
        TimeSpan small = TimeSpan.FromHours(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => Strategy.TimeSpans(big, small));
    }
}
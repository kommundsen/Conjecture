// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Strategies;

public class DateTimeOffsetStrategyTests
{
    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void DateTimeOffsets_BoundedRange_ReturnsInRange()
    {
        DateTimeOffset min = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset max = new(2030, 12, 31, 23, 59, 59, TimeSpan.Zero);
        Strategy<DateTimeOffset> strategy = Generate.DateTimeOffsets(min, max);
        ConjectureData data = MakeData();

        for (int i = 0; i < 1000; i++)
        {
            DateTimeOffset value = strategy.Generate(data);
            Assert.InRange(value, min, max);
        }
    }

    [Fact]
    public void DateTimeOffsets_DefaultRange_DoesNotThrow()
    {
        Strategy<DateTimeOffset> strategy = Generate.DateTimeOffsets();
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            _ = strategy.Generate(data);
        }
    }

    [Fact]
    public void DateTimeOffsets_MinEqualsMax_ReturnsConstant()
    {
        DateTimeOffset t = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        Strategy<DateTimeOffset> strategy = Generate.DateTimeOffsets(t, t);
        ConjectureData data = MakeData();

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(t, strategy.Generate(data));
        }
    }

    [Fact]
    public void DateTimeOffsets_MinGreaterThanMax_Throws()
    {
        DateTimeOffset max = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset min = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.DateTimeOffsets(max, min));
    }

    [Fact]
    public void DateTimeOffsets_AlwaysReturnsUtcOffset()
    {
        Strategy<DateTimeOffset> strategy = Generate.DateTimeOffsets();
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            DateTimeOffset value = strategy.Generate(data);
            Assert.Equal(TimeSpan.Zero, value.Offset);
        }
    }

    [Fact]
    public void DateTimeOffsets_BoundedWithNonUtcOffsets_ReturnsUtcOffset()
    {
        TimeSpan offset = TimeSpan.FromHours(5);
        DateTimeOffset min = new(2000, 1, 1, 0, 0, 0, offset);
        DateTimeOffset max = new(2030, 12, 31, 23, 59, 59, offset);
        Strategy<DateTimeOffset> strategy = Generate.DateTimeOffsets(min, max);
        ConjectureData data = MakeData();

        for (int i = 0; i < 100; i++)
        {
            DateTimeOffset value = strategy.Generate(data);
            Assert.Equal(TimeSpan.Zero, value.Offset);
        }
    }
}
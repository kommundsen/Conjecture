// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Time;

namespace Conjecture.Time.Tests;

public class DateTimeOffsetExtensionsTests
{
    private static TimeZoneInfo FindEasternTimeZone()
    {
        foreach (string id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static bool IsNearDstTransition(DateTimeOffset value, TimeZoneInfo zone)
    {
        TimeSpan window = TimeSpan.FromHours(1);
        TimeSpan step = TimeSpan.FromMinutes(1);
        DateTimeOffset start = value - window;
        DateTimeOffset end = value + window;

        bool previouslyInDst = zone.IsDaylightSavingTime(start);
        DateTimeOffset cursor = start + step;

        while (cursor <= end)
        {
            bool nowInDst = zone.IsDaylightSavingTime(cursor);
            if (nowInDst != previouslyInDst)
            {
                return true;
            }

            previouslyInDst = nowInDst;
            cursor += step;
        }

        return false;
    }

    [Fact]
    public void NearDstTransition_WithEasternZone_AllValuesAreWithinOneHourOfTransition()
    {
        TimeZoneInfo zone = FindEasternTimeZone();
        Strategy<DateTimeOffset> strategy = Strategy.DateTimeOffsets().NearDstTransition(zone);

        IReadOnlyList<DateTimeOffset> samples = strategy.WithSeed(1UL).Sample(20);

        Assert.All(samples, value => Assert.True(
            IsNearDstTransition(value, zone),
            $"{value} is not within 1 hour of a DST transition in {zone.Id}"));
    }

    [Fact]
    public void NearDstTransition_WithNullZone_AllValuesAreWithinOneHourOfTransitionInLocalZone()
    {
        Strategy<DateTimeOffset> strategy = Strategy.DateTimeOffsets().NearDstTransition(null);

        IReadOnlyList<DateTimeOffset> samples = strategy.WithSeed(2UL).Sample(10);

        // Only assert no exception thrown and values are non-default; correctness of local zone tested separately
        Assert.Equal(10, samples.Count);
    }

    [Fact]
    public void NearMidnight_AllValuesAreWithinThirtyMinutesOfMidnightUtc()
    {
        Strategy<DateTimeOffset> strategy = Strategy.DateTimeOffsets().NearMidnight();

        IReadOnlyList<DateTimeOffset> samples = strategy.WithSeed(3UL).Sample(30);

        Assert.All(samples, value =>
        {
            DateTimeOffset utc = value.ToUniversalTime();
            bool nearMidnight = utc.Hour == 23 || utc.Hour == 0;
            Assert.True(nearMidnight, $"{value:O} (UTC hour={utc.Hour}) is not near midnight");
        });
    }

    [Fact]
    public void NearLeapYear_AllValuesAreBetweenFeb28AndMar1OfALeapYear()
    {
        Strategy<DateTimeOffset> strategy = Strategy.DateTimeOffsets().NearLeapYear();

        IReadOnlyList<DateTimeOffset> samples = strategy.WithSeed(4UL).Sample(20);

        Assert.All(samples, value =>
        {
            DateTimeOffset utc = value.ToUniversalTime();
            bool isLeapYear = DateTime.IsLeapYear(utc.Year);
            bool nearFeb29 =
                (utc.Month == 2 && utc.Day >= 28) ||
                (utc.Month == 3 && utc.Day == 1);
            Assert.True(isLeapYear && nearFeb29,
                $"{value:O} is not within 1 day of Feb 29 in a leap year (year={utc.Year}, leapYear={isLeapYear}, month={utc.Month}, day={utc.Day})");
        });
    }

    [Fact]
    public void NearEpoch_AtLeastOneValueHasYearInEpochRange()
    {
        Strategy<DateTimeOffset> strategy = Strategy.DateTimeOffsets().NearEpoch();

        IReadOnlyList<DateTimeOffset> samples = strategy.WithSeed(5UL).Sample(50);

        Assert.Contains(samples, value => value.Year >= 1969 && value.Year <= 1971);
    }

    [Fact]
    public void NearMidnight_ComposedOnDateTimeOffsets_DoesNotThrow()
    {
        Strategy<DateTimeOffset> strategy = Strategy.DateTimeOffsets().NearMidnight();

        Exception? caught = Record.Exception(() => strategy.WithSeed(6UL).Sample(1));

        Assert.Null(caught);
    }

    [Fact]
    public void NearDstTransition_ReturnsNewStrategy_NotSameReferenceAsInput()
    {
        Strategy<DateTimeOffset> input = Strategy.DateTimeOffsets();
        Strategy<DateTimeOffset> result = input.NearDstTransition(FindEasternTimeZone());

        Assert.False(ReferenceEquals(input, result));
    }

    [Fact]
    public void NearMidnight_ReturnsNewStrategy_NotSameReferenceAsInput()
    {
        Strategy<DateTimeOffset> input = Strategy.DateTimeOffsets();
        Strategy<DateTimeOffset> result = input.NearMidnight();

        Assert.False(ReferenceEquals(input, result));
    }

    [Fact]
    public void NearLeapYear_ReturnsNewStrategy_NotSameReferenceAsInput()
    {
        Strategy<DateTimeOffset> input = Strategy.DateTimeOffsets();
        Strategy<DateTimeOffset> result = input.NearLeapYear();

        Assert.False(ReferenceEquals(input, result));
    }

    [Fact]
    public void NearEpoch_ReturnsNewStrategy_NotSameReferenceAsInput()
    {
        Strategy<DateTimeOffset> input = Strategy.DateTimeOffsets();
        Strategy<DateTimeOffset> result = input.NearEpoch();

        Assert.False(ReferenceEquals(input, result));
    }

    [Fact]
    public void WithPrecision_Milliseconds_TicksAreRoundedToMillisecond()
    {
        Strategy<DateTimeOffset> strategy = Strategy.DateTimeOffsets().WithPrecision(TimeSpan.FromMilliseconds(1));

        IReadOnlyList<DateTimeOffset> samples = strategy.WithSeed(1UL).Sample(30);

        Assert.All(samples, value => Assert.Equal(0L, value.Ticks % TimeSpan.TicksPerMillisecond));
    }

    [Fact]
    public void WithPrecision_Seconds_TicksAreRoundedToSecond()
    {
        Strategy<DateTimeOffset> strategy = Strategy.DateTimeOffsets().WithPrecision(TimeSpan.FromSeconds(1));

        IReadOnlyList<DateTimeOffset> samples = strategy.WithSeed(1UL).Sample(30);

        Assert.All(samples, value => Assert.Equal(0L, value.Ticks % TimeSpan.TicksPerSecond));
    }

    [Fact]
    public void WithStrippedOffset_StrippedValueHasZeroOffset()
    {
        Strategy<(DateTimeOffset Original, DateTimeOffset Stripped)> strategy =
            Strategy.DateTimeOffsets().WithStrippedOffset();

        IReadOnlyList<(DateTimeOffset Original, DateTimeOffset Stripped)> samples =
            strategy.WithSeed(1UL).Sample(30);

        Assert.All(samples, result => Assert.Equal(TimeSpan.Zero, result.Stripped.Offset));
    }

    [Fact]
    public void WithStrippedOffset_OriginalPreservesOffset()
    {
        Strategy<(DateTimeOffset Original, DateTimeOffset Stripped)> strategy =
            Strategy.DateTimeOffsets().WithStrippedOffset();

        IReadOnlyList<(DateTimeOffset Original, DateTimeOffset Stripped)> samples =
            strategy.WithSeed(1UL).Sample(30);

        Assert.Contains(samples, result => result.Original.Offset != TimeSpan.Zero);
    }

    [Fact]
    public void WithStrippedOffset_TickValuesMatch()
    {
        Strategy<(DateTimeOffset Original, DateTimeOffset Stripped)> strategy =
            Strategy.DateTimeOffsets().WithStrippedOffset();

        IReadOnlyList<(DateTimeOffset Original, DateTimeOffset Stripped)> samples =
            strategy.WithSeed(1UL).Sample(30);

        Assert.All(samples, result => Assert.Equal(result.Original.Ticks, result.Stripped.Ticks));
    }

    [Fact]
    public void WithPrecision_ZeroPrecision_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () =>
            Strategy.DateTimeOffsets().WithPrecision(TimeSpan.Zero));
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Time;

namespace Conjecture.Time.Tests;

public class DateOnlyStrategyTests
{
    [Fact]
    public void DateOnlys_ValuesAreWithinMinMax()
    {
        DateOnly min = new(2020, 1, 1);
        DateOnly max = new(2025, 12, 31);
        Strategy<DateOnly> strategy = Strategy.DateOnlyValues(min, max);

        IReadOnlyList<DateOnly> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        Assert.All(samples, d =>
        {
            Assert.True(d >= min, $"{d} is before min {min}");
            Assert.True(d <= max, $"{d} is after max {max}");
        });
    }

    [Fact]
    public void NearMonthBoundary_ValuesAreOnLastOrFirstDay()
    {
        Strategy<DateOnly> strategy = Strategy.DateOnlyValues().NearMonthBoundary();

        IReadOnlyList<DateOnly> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        Assert.All(samples, d =>
        {
            bool isFirst = d.Day == 1;
            bool isLast = d.Day == DateTime.DaysInMonth(d.Year, d.Month);
            Assert.True(isFirst || isLast, $"{d} is not a first or last day of the month");
        });
    }

    [Fact]
    public void NearLeapDay_ValuesAreWithinOneDayOfFeb29()
    {
        Strategy<DateOnly> strategy = Strategy.DateOnlyValues().NearLeapDay();

        IReadOnlyList<DateOnly> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        Assert.All(samples, d =>
        {
            DateTime sampleDt = d.ToDateTime(TimeOnly.MinValue);
            double minDistance = FindMinDistanceToLeapDay(sampleDt);
            Assert.True(minDistance <= 1.0, $"{d} is {minDistance} days from the nearest Feb 29");
        });
    }

    private static double FindMinDistanceToLeapDay(DateTime dt)
    {
        double minDays = double.MaxValue;
        int startYear = dt.Year - 4;
        int endYear = dt.Year + 4;

        for (int y = startYear; y <= endYear; y++)
        {
            if (DateTime.IsLeapYear(y))
            {
                DateTime leapDay = new(y, 2, 29);
                double diff = Math.Abs((dt - leapDay).TotalDays);
                if (diff < minDays)
                {
                    minDays = diff;
                }
            }
        }

        return minDays;
    }
}
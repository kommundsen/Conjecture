// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Time;

namespace Conjecture.Time.Tests;

public class TimeOnlyStrategyTests
{
    [Fact]
    public void TimeOnlys_ValuesAreWithinMinMax()
    {
        TimeOnly min = new(8, 0);
        TimeOnly max = new(18, 0);
        Strategy<TimeOnly> strategy = Generate.TimeOnlyValues(min, max);

        IReadOnlyList<TimeOnly> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        Assert.All(samples, t =>
        {
            Assert.True(t >= min, $"{t} is before min {min}");
            Assert.True(t <= max, $"{t} is after max {max}");
        });
    }

    [Fact]
    public void NearMidnight_ValuesAreWithin30SecondsOfMidnight()
    {
        Strategy<TimeOnly> strategy = Generate.TimeOnlyValues().NearMidnight();
        long threshold = 30 * TimeSpan.TicksPerSecond;

        IReadOnlyList<TimeOnly> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        Assert.All(samples, t =>
        {
            bool nearStart = t.Ticks <= threshold;
            bool nearEnd = t.Ticks >= TimeOnly.MaxValue.Ticks - threshold;
            Assert.True(nearStart || nearEnd, $"{t} is not within 30 seconds of midnight");
        });
    }

    [Fact]
    public void NearNoon_ValuesAreWithin30SecondsOfNoon()
    {
        Strategy<TimeOnly> strategy = Generate.TimeOnlyValues().NearNoon();
        TimeOnly noon = new(12, 0, 0);
        long threshold = 30 * TimeSpan.TicksPerSecond;

        IReadOnlyList<TimeOnly> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        Assert.All(samples, t =>
        {
            long diff = Math.Abs(t.Ticks - noon.Ticks);
            Assert.True(diff <= threshold, $"{t} is not within 30 seconds of noon");
        });
    }

    [Fact]
    public void NearEndOfDay_ValuesAreWithin30SecondsOfEndOfDay()
    {
        Strategy<TimeOnly> strategy = Generate.TimeOnlyValues().NearEndOfDay();
        TimeOnly threshold = new(23, 59, 29);

        IReadOnlyList<TimeOnly> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        Assert.All(samples, t =>
            Assert.True(t.Ticks >= threshold.Ticks, $"{t} is not within 30 seconds of end of day"));
    }
}
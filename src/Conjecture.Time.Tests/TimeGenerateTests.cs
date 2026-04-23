// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Time;

using Microsoft.Extensions.Time.Testing;

namespace Conjecture.Time.Tests;

public class TimeGenerateExtensionsTests
{
    [Fact]
    public void TimeZones_ReturnsStrategy()
    {
        Strategy<TimeZoneInfo> strategy = Generate.TimeZones();

        Assert.NotNull(strategy);
    }

    [Fact]
    public void TimeZones_GeneratesOnlySystemZones()
    {
        Strategy<TimeZoneInfo> strategy = Generate.TimeZones();
        System.Collections.ObjectModel.ReadOnlyCollection<TimeZoneInfo> systemZones = TimeZoneInfo.GetSystemTimeZones();
        System.Collections.Generic.HashSet<string> systemIds = [.. systemZones.Select(static z => z.Id)];

        IReadOnlyList<TimeZoneInfo> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, z => Assert.Contains(z.Id, systemIds));
    }

    [Fact]
    public void ClockSet_ReturnsArrayOfExactNodeCount()
    {
        Strategy<FakeTimeProvider[]> strategy = Generate.ClockSet(3, TimeSpan.FromSeconds(5));

        FakeTimeProvider[] clocks = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal(3, clocks.Length);
    }

    [Fact]
    public void ClockSet_EachClockIsWithinMaxSkewOfOthers()
    {
        TimeSpan maxSkew = TimeSpan.FromSeconds(1);
        Strategy<FakeTimeProvider[]> strategy = Generate.ClockSet(3, maxSkew);

        FakeTimeProvider[] clocks = DataGen.SampleOne(strategy, seed: 1UL);

        for (int i = 0; i < clocks.Length; i++)
        {
            for (int j = i + 1; j < clocks.Length; j++)
            {
                TimeSpan diff = (clocks[i].GetUtcNow() - clocks[j].GetUtcNow()).Duration();
                Assert.True(diff <= maxSkew, $"Clock {i} and clock {j} differ by {diff}, exceeding maxSkew {maxSkew}");
            }
        }
    }

    [Fact]
    public void ClockSet_NodeCountLessThanTwo_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            static () => Generate.ClockSet(1, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void ClockSet_ClocksAreIndependent()
    {
        Strategy<FakeTimeProvider[]> strategy = Generate.ClockSet(3, TimeSpan.FromSeconds(5));

        FakeTimeProvider[] clocks = DataGen.SampleOne(strategy, seed: 1UL);

        DateTimeOffset before0 = clocks[0].GetUtcNow();
        DateTimeOffset before1 = clocks[1].GetUtcNow();
        DateTimeOffset before2 = clocks[2].GetUtcNow();

        clocks[0].Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(before1, clocks[1].GetUtcNow());
        Assert.Equal(before2, clocks[2].GetUtcNow());
    }

    [Fact]
    public void ClockSet_EachElementIsFakeTimeProvider()
    {
        Strategy<FakeTimeProvider[]> strategy = Generate.ClockSet(2, TimeSpan.FromSeconds(1));

        FakeTimeProvider[] clocks = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.All(clocks, static c => Assert.IsType<FakeTimeProvider>(c));
    }

    [Fact]
    public void IanaZoneIds_AllValuesHaveIanaIds()
    {
        Strategy<string> strategy = Generate.IanaZoneIds();

        IReadOnlyList<string> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, static id => TimeZoneInfo.FindSystemTimeZoneById(id));
    }

    [Fact]
    public void IanaZoneIds_WithPreferDst_AllZonesSupportDst()
    {
        Strategy<string> strategy = Generate.IanaZoneIds(preferDst: true);

        IReadOnlyList<string> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, static id =>
        {
            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            Assert.True(tz.SupportsDaylightSavingTime, $"Zone '{id}' does not support DST");
        });
    }

    [Fact]
    public void WindowsZoneIds_AllValuesAreWindowsZoneIds()
    {
        Strategy<string> strategy = Generate.WindowsZoneIds();

        IReadOnlyList<string> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, static id =>
        {
            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            Assert.False(tz.HasIanaId, $"Zone '{id}' has an IANA id, expected a Windows-style id");
        });
    }

    [Fact]
    public void TimeZone_WithPreferDst_AllZonesSupportDst()
    {
        Strategy<TimeZoneInfo> strategy = Generate.TimeZone(preferDst: true);

        IReadOnlyList<TimeZoneInfo> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, static tz =>
            Assert.True(tz.SupportsDaylightSavingTime, $"Zone '{tz.Id}' does not support DST"));
    }

    [Fact]
    public void IanaZoneIds_ProducesVariety()
    {
        Strategy<string> strategy = Generate.IanaZoneIds();

        IReadOnlyList<string> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        System.Collections.Generic.HashSet<string> distinct = [.. samples];
        Assert.True(distinct.Count >= 3, $"Expected at least 3 distinct IANA zone IDs, got {distinct.Count}");
    }
}
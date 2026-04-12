// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

using Microsoft.Extensions.Time.Testing;

namespace Conjecture.Time;

/// <summary>Factory methods for generating time-related test values.</summary>
public static class TimeGenerate
{
    // UTC at index 0 so SampledFrom shrinks toward it; deduplicated in case GetSystemTimeZones already includes UTC.
    private static readonly TimeZoneInfo[] SystemTimeZones = BuildSystemTimeZones();

    private static TimeZoneInfo[] BuildSystemTimeZones()
    {
        // UTC is placed first so SampledFrom shrinks toward it; non-UTC zones follow in system order.
        System.Collections.ObjectModel.ReadOnlyCollection<TimeZoneInfo> zones = TimeZoneInfo.GetSystemTimeZones();
        List<TimeZoneInfo> result = [TimeZoneInfo.Utc];
        foreach (TimeZoneInfo tz in zones)
        {
            if (tz.Id != TimeZoneInfo.Utc.Id)
            {
                result.Add(tz);
            }
        }

        return [.. result];
    }

    /// <summary>Returns a strategy that picks uniformly from the system time zones, shrinking toward UTC.</summary>
    public static Strategy<TimeZoneInfo> TimeZones()
    {
        return Generate.SampledFrom(SystemTimeZones);
    }

    /// <summary>
    /// Returns a strategy that generates an array of <paramref name="nodeCount"/> independent
    /// <see cref="FakeTimeProvider"/> instances, each with a clock skew drawn from
    /// [<c>-maxSkew/2</c>, <c>+maxSkew/2</c>] relative to <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    /// <param name="nodeCount">Number of clocks to generate. Must be at least 2.</param>
    /// <param name="maxSkew">Maximum pairwise clock difference.</param>
    public static Strategy<FakeTimeProvider[]> ClockSet(int nodeCount, TimeSpan maxSkew)
    {
        if (nodeCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeCount), nodeCount, "nodeCount must be at least 2.");
        }

        long halfSkewTicks = maxSkew.Ticks / 2;
        Strategy<long> skewStrategy = Generate.Integers<long>(-halfSkewTicks, halfSkewTicks);

        return Generate.Compose<FakeTimeProvider[]>(ctx =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            FakeTimeProvider[] clocks = new FakeTimeProvider[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                long skewTicks = ctx.Generate(skewStrategy);
                clocks[i] = new FakeTimeProvider(now + TimeSpan.FromTicks(skewTicks));
            }

            return clocks;
        });
    }
}

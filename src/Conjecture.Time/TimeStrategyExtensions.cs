// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

using Microsoft.Extensions.Time.Testing;

namespace Conjecture.Time;

/// <summary>Extension methods on <see cref="Strategy"/> for time-related test value generation.</summary>
public static class TimeStrategyExtensions
{
    // UTC at index 0 so SampledFrom shrinks toward it; deduplicated in case GetSystemTimeZones already includes UTC.
    private static readonly TimeZoneInfo[] SystemTimeZones = BuildSystemTimeZones();

    private static readonly TimeZoneInfo[] CrossPlatformZones = BuildCrossPlatformZones();
    private static readonly string[] IanaDstIds = BuildIanaDstIds();
    private static readonly string[] WindowsIds = BuildWindowsIds();
    private static readonly TimeZoneInfo[] CrossPlatformDstZones = BuildCrossPlatformDstZones();

    private static readonly Strategy<DateTimeOffset> ClockStartStrategy = Strategy.DateTimeOffsets(
        new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2050, 1, 1, 0, 0, 0, TimeSpan.Zero));

    extension(Strategy)
    {
        /// <summary>Returns a strategy that picks uniformly from the system time zones, shrinking toward UTC.</summary>
        public static Strategy<TimeZoneInfo> TimeZones()
        {
            return Strategy.SampledFrom(TimeStrategyExtensions.SystemTimeZones);
        }

        /// <summary>Returns a strategy that samples IANA timezone IDs from a cross-platform-safe subset.</summary>
        public static Strategy<string> IanaZoneIds(bool preferDst = false)
        {
            return preferDst
                ? Strategy.SampledFrom(TimeStrategyExtensions.IanaDstIds)
                : Strategy.SampledFrom(CrossPlatformTimeZones.IanaIds);
        }

        /// <summary>Returns a strategy that samples Windows timezone IDs from a cross-platform-safe subset.</summary>
        public static Strategy<string> WindowsZoneIds()
        {
            return Strategy.SampledFrom(TimeStrategyExtensions.WindowsIds);
        }

        /// <summary>Returns a strategy that samples TimeZoneInfo from the cross-platform-safe subset, optionally filtering to DST zones.</summary>
        public static Strategy<TimeZoneInfo> TimeZone(bool preferDst = false)
        {
            return preferDst
                ? Strategy.SampledFrom(TimeStrategyExtensions.CrossPlatformDstZones)
                : Strategy.SampledFrom(TimeStrategyExtensions.CrossPlatformZones);
        }

        /// <summary>
        /// Returns a strategy that generates an array of <paramref name="nodeCount"/> independent
        /// <see cref="FakeTimeProvider"/> instances, each with a clock skew generated from
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
            Strategy<long> skewStrategy = Strategy.Integers<long>(-halfSkewTicks, halfSkewTicks);

            return Strategy.Compose<FakeTimeProvider[]>(ctx =>
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

        /// <summary>
        /// Returns a strategy that generates a <see cref="FakeTimeProvider"/> pre-positioned at a
        /// random time within <paramref name="maxJump"/> of the 2000-01-01 UTC anchor.
        /// </summary>
        /// <param name="maxJump">Width of the time window. Must be positive.</param>
        public static Strategy<FakeTimeProvider> AdvancingClocks(TimeSpan maxJump)
        {
            if (maxJump <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxJump), maxJump, "maxJump must be positive.");
            }

            DateTimeOffset anchor = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Strategy<DateTimeOffset> startStrategy = Strategy.DateTimeOffsets(anchor, anchor + maxJump);

            return Strategy.Compose<FakeTimeProvider>(ctx =>
            {
                DateTimeOffset start = ctx.Generate(startStrategy);
                return new FakeTimeProvider(start);
            });
        }

        /// <summary>
        /// Returns a strategy that generates a <see cref="FakeTimeProvider"/> paired with
        /// <paramref name="advanceCount"/> time advances, each in
        /// [<paramref name="allowBackward"/> ? <c>-maxJump</c> : <c>Zero</c>, <c>maxJump</c>].
        /// </summary>
        /// <param name="advanceCount">Number of advances to generate.</param>
        /// <param name="maxJump">Maximum magnitude of each advance.</param>
        /// <param name="allowBackward">When <see langword="true"/>, advances may be negative.</param>
        public static Strategy<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)> ClockWithAdvances(
            int advanceCount, TimeSpan maxJump, bool allowBackward = false)
        {
            if (advanceCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(advanceCount), advanceCount, "advanceCount must be at least 1.");
            }

            long minTicks = allowBackward ? -maxJump.Ticks : 0L;
            Strategy<long> jumpStrategy = Strategy.Integers<long>(minTicks, maxJump.Ticks);

            return Strategy.Compose<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)>(ctx =>
            {
                DateTimeOffset start = ctx.Generate(TimeStrategyExtensions.ClockStartStrategy);
                FakeTimeProvider clock = new(start);
                TimeSpan[] advances = new TimeSpan[advanceCount];
                for (int i = 0; i < advanceCount; i++)
                {
                    advances[i] = TimeSpan.FromTicks(ctx.Generate(jumpStrategy));
                }
                return (clock, advances);
            });
        }

        /// <summary>
        /// Returns a strategy that generates a <see cref="RecurringEventSample"/> by walking
        /// <paramref name="nextOccurrence"/> from a random window start until <paramref name="window"/> elapses.
        /// </summary>
        public static Strategy<RecurringEventSample> RecurringEvents(
            Func<DateTimeOffset, DateTimeOffset?> nextOccurrence,
            TimeZoneInfo zone,
            TimeSpan window)
        {
            return Strategy.Compose<RecurringEventSample>(ctx =>
            {
                DateTimeOffset windowStart = ctx.Generate(TimeStrategyExtensions.ClockStartStrategy);
                DateTimeOffset windowEnd = windowStart + window;
                List<DateTimeOffset> occurrences = [];
                DateTimeOffset? current = nextOccurrence(windowStart);
                int steps = 0;
                while (current is not null && current.Value <= windowEnd)
                {
                    if (++steps > 10_000)
                    {
                        throw new InvalidOperationException(
                            "nextOccurrence did not advance past the window after 10 000 steps. " +
                            "Ensure the delegate always returns a value strictly after its input.");
                    }

                    if (current.Value >= windowStart)
                    {
                        occurrences.Add(current.Value);
                    }

                    current = nextOccurrence(current.Value);
                }
                return new RecurringEventSample(windowStart, windowEnd, occurrences.AsReadOnly(), zone, nextOccurrence);
            });
        }
    }

    private static string[] BuildIanaDstIds()
    {
        List<string> result = [];
        foreach (TimeZoneInfo tz in CrossPlatformZones)
        {
            if (tz.SupportsDaylightSavingTime)
            {
                result.Add(tz.Id);
            }
        }
        return [.. result];
    }

    private static string[] BuildWindowsIds()
    {
        HashSet<string> seen = [];
        List<string> result = [];
        foreach (string ianaId in CrossPlatformTimeZones.IanaIds)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out string? winId)
                && winId != ianaId
                && seen.Add(winId))
            {
                result.Add(winId);
            }
        }
        return [.. result];
    }

    private static TimeZoneInfo[] BuildCrossPlatformZones()
    {
        List<TimeZoneInfo> result = [];
        foreach (string id in CrossPlatformTimeZones.IanaIds)
        {
            result.Add(TimeZoneInfo.FindSystemTimeZoneById(id));
        }
        return [.. result];
    }

    private static TimeZoneInfo[] BuildCrossPlatformDstZones()
    {
        List<TimeZoneInfo> result = [];
        foreach (TimeZoneInfo tz in CrossPlatformZones)
        {
            if (tz.SupportsDaylightSavingTime)
            {
                result.Add(tz);
            }
        }
        return [.. result];
    }

    private static TimeZoneInfo[] BuildSystemTimeZones()
    {
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
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Time;

/// <summary>Extension methods on <see cref="Strategy{T}"/> for <see cref="DateTimeOffset"/> edge-case generation.</summary>
public static class DateTimeOffsetExtensions
{
    // Cached once per process — system time zones and adjustment rules are stable at runtime.
    private static readonly List<TimeZoneInfo> ZonesWithDst = BuildZonesWithDst();
    private static readonly int CurrentYear = DateTimeOffset.UtcNow.Year;

    private static readonly DateTimeOffset[] EpochAnchors =
    [
        new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new(1, 1, 2, 0, 0, 0, TimeSpan.Zero),
        new(9999, 12, 30, 0, 0, 0, TimeSpan.Zero),
        new(2038, 1, 19, 0, 0, 0, TimeSpan.Zero),
    ];

    extension(Strategy<DateTimeOffset> s)
    {
        /// <summary>Returns a strategy that generates values within ±30 minutes of midnight UTC.</summary>
        public Strategy<DateTimeOffset> NearMidnight()
        {
            return Generate.Compose<DateTimeOffset>(ctx =>
            {
                DateTimeOffset base_ = ctx.Generate(s);
                DateTimeOffset utcDate = new(base_.UtcDateTime.Date, TimeSpan.Zero);
                long jitterTicks = ctx.Generate(Generate.Integers<long>(
                    -TimeSpan.FromMinutes(30).Ticks,
                    TimeSpan.FromMinutes(30).Ticks));
                return utcDate.AddTicks(jitterTicks);
            });
        }

        /// <summary>Returns a strategy that generates values within ±1 day of Feb 29 in a leap year.</summary>
        public Strategy<DateTimeOffset> NearLeapYear()
        {
            return Generate.Compose<DateTimeOffset>(ctx =>
            {
                int year = ctx.Generate(Generate.Integers<int>(1970, 2400));
                ctx.Assume(DateTime.IsLeapYear(year));
                DateTimeOffset feb29 = new(year, 2, 29, 0, 0, 0, TimeSpan.Zero);
                long jitterTicks = ctx.Generate(Generate.Integers<long>(
                    -TimeSpan.FromDays(1).Ticks,
                    TimeSpan.FromDays(1).Ticks));
                return feb29.AddTicks(jitterTicks);
            });
        }

        /// <summary>Returns a strategy that generates values near well-known epoch anchors.</summary>
        public Strategy<DateTimeOffset> NearEpoch()
        {
            long maxJitter = TimeSpan.FromHours(1).Ticks;

            return Generate.Compose<DateTimeOffset>(ctx =>
            {
                int index = ctx.Generate(Generate.Integers<int>(0, EpochAnchors.Length - 1));
                DateTimeOffset anchor = EpochAnchors[index];
                long jitterTicks = ctx.Generate(Generate.Integers<long>(-maxJitter, maxJitter));
                long clampedTicks = Math.Clamp(
                    anchor.Ticks + jitterTicks,
                    DateTimeOffset.MinValue.Ticks,
                    DateTimeOffset.MaxValue.Ticks);
                return new DateTimeOffset(clampedTicks, TimeSpan.Zero);
            });
        }

        /// <summary>Returns a strategy that generates values within ±1 hour of a DST transition in <paramref name="zone"/>.</summary>
        public Strategy<DateTimeOffset> NearDstTransition(TimeZoneInfo? zone = null)
        {
            return Generate.Compose<DateTimeOffset>(ctx =>
            {
                TimeZoneInfo resolvedZone = zone ?? PickZoneWithRules(ctx);
                List<DateTimeOffset> transitions = GetTransitions(resolvedZone);

                if (transitions.Count == 0)
                {
                    return ctx.Generate(s.NearEpoch());
                }

                int index = ctx.Generate(Generate.Integers<int>(0, transitions.Count - 1));
                DateTimeOffset transition = transitions[index];
                long jitterTicks = ctx.Generate(Generate.Integers<long>(
                    -TimeSpan.FromHours(1).Ticks,
                    TimeSpan.FromHours(1).Ticks));
                return transition.AddTicks(jitterTicks);
            });
        }

        /// <summary>
        /// Returns a strategy that truncates each generated value to <paramref name="precision"/>,
        /// simulating provider-imposed precision stripping (e.g. SQL Server datetime2(3) rounds to milliseconds).
        /// </summary>
        public Strategy<DateTimeOffset> WithPrecision(TimeSpan precision)
        {
            return precision.Ticks <= 0
                ? throw new ArgumentOutOfRangeException(nameof(precision), precision, "precision must be positive.")
                : s.Select(dto => new DateTimeOffset(dto.Ticks - dto.Ticks % precision.Ticks, dto.Offset));
        }

        /// <summary>
        /// Returns a strategy that pairs each generated value with its offset-stripped counterpart (Offset → Zero),
        /// simulating providers that lose the UTC offset on roundtrip.
        /// </summary>
        public Strategy<(DateTimeOffset Original, DateTimeOffset Stripped)> WithStrippedOffset()
        {
            return Generate.Compose<(DateTimeOffset Original, DateTimeOffset Stripped)>(ctx =>
            {
                DateTimeOffset dto = ctx.Generate(s);
                int offsetMinutes = ctx.Generate(Generate.Integers<int>(-840, 840));
                TimeSpan offset = TimeSpan.FromMinutes(offsetMinutes);
                DateTimeOffset original = new(dto.Ticks, offset);
                DateTimeOffset stripped = new(dto.Ticks, TimeSpan.Zero);
                return (original, stripped);
            });
        }
    }

    private static TimeZoneInfo PickZoneWithRules(IGenerationContext ctx)
    {
        if (ZonesWithDst.Count == 0)
        {
            return TimeZoneInfo.Utc;
        }

        int index = ctx.Generate(Generate.Integers<int>(0, ZonesWithDst.Count - 1));
        return ZonesWithDst[index];
    }

    private static List<DateTimeOffset> GetTransitions(TimeZoneInfo zone)
        => DstTransitionHelper.GetTransitionsUtc(zone, yearMinus: 1, yearPlus: 1);

    private static List<TimeZoneInfo> BuildZonesWithDst()
    {
        List<TimeZoneInfo> result = [];
        foreach (TimeZoneInfo tz in TimeZoneInfo.GetSystemTimeZones())
        {
            if (tz.GetAdjustmentRules().Length > 0)
            {
                result.Add(tz);
            }
        }

        return result;
    }
}
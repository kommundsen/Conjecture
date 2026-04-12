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
    }

    private static TimeZoneInfo PickZoneWithRules(IGeneratorContext ctx)
    {
        if (ZonesWithDst.Count == 0)
        {
            return TimeZoneInfo.Utc;
        }

        int index = ctx.Generate(Generate.Integers<int>(0, ZonesWithDst.Count - 1));
        return ZonesWithDst[index];
    }

    private static List<DateTimeOffset> GetTransitions(TimeZoneInfo zone)
    {
        List<DateTimeOffset> transitions = [];

        foreach (TimeZoneInfo.AdjustmentRule rule in zone.GetAdjustmentRules())
        {
            for (int year = CurrentYear - 1; year <= CurrentYear + 1; year++)
            {
                if (rule.DateStart.Year > year || rule.DateEnd.Year < year)
                {
                    continue;
                }

                // DaylightTransitionStart is expressed in standard time; subtract standard offset to get UTC.
                // DaylightTransitionEnd is expressed in DST time; subtract DST offset to get UTC.
                TimeSpan standardOffset = zone.BaseUtcOffset;
                TimeSpan dstOffset = zone.BaseUtcOffset + rule.DaylightDelta;

                DateTimeOffset? start = TransitionToUtc(rule.DaylightTransitionStart, year, standardOffset);
                DateTimeOffset? end = TransitionToUtc(rule.DaylightTransitionEnd, year, dstOffset);

                if (start is not null)
                {
                    transitions.Add(start.Value);
                }

                if (end is not null)
                {
                    transitions.Add(end.Value);
                }
            }
        }

        return transitions;
    }

    private static DateTimeOffset? TransitionToUtc(TimeZoneInfo.TransitionTime transition, int year, TimeSpan localOffset)
    {
        try
        {
            DateTime localDt = transition.IsFixedDateRule
                ? new DateTime(year, transition.Month, transition.Day,
                    transition.TimeOfDay.Hour, transition.TimeOfDay.Minute, transition.TimeOfDay.Second)
                : GetFloatingTransitionDate(year, transition);

            DateTime utcDt = localDt - localOffset;
            return new DateTimeOffset(utcDt, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static DateTime GetFloatingTransitionDate(int year, TimeZoneInfo.TransitionTime transition)
    {
        int firstDayOfMonth = (int)new DateTime(year, transition.Month, 1).DayOfWeek;
        int targetDay = (int)transition.DayOfWeek;
        int offset = (targetDay - firstDayOfMonth + 7) % 7;
        int day = 1 + offset + ((transition.Week - 1) * 7);

        int daysInMonth = DateTime.DaysInMonth(year, transition.Month);
        while (day > daysInMonth)
        {
            day -= 7;
        }

        return new DateTime(year, transition.Month, day,
            transition.TimeOfDay.Hour, transition.TimeOfDay.Minute, transition.TimeOfDay.Second);
    }

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

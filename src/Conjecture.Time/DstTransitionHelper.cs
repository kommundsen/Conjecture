// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Time;

internal static class DstTransitionHelper
{
    internal static List<DateTimeOffset> GetTransitionsUtc(TimeZoneInfo zone, int yearMinus, int yearPlus)
    {
        List<DateTimeOffset> transitions = [];
        int currentYear = DateTimeOffset.UtcNow.Year;

        foreach (TimeZoneInfo.AdjustmentRule rule in zone.GetAdjustmentRules())
        {
            for (int year = currentYear - yearMinus; year <= currentYear + yearPlus; year++)
            {
                if (rule.DateStart.Year > year || rule.DateEnd.Year < year)
                {
                    continue;
                }

                TimeSpan standardOffset = zone.BaseUtcOffset;
                TimeSpan dstOffset = zone.BaseUtcOffset + rule.DaylightDelta;

                DateTimeOffset? start = ToUtc(rule.DaylightTransitionStart, year, standardOffset);
                DateTimeOffset? end = ToUtc(rule.DaylightTransitionEnd, year, dstOffset);

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

    private static DateTimeOffset? ToUtc(TimeZoneInfo.TransitionTime transition, int year, TimeSpan localOffset)
    {
        try
        {
            DateTime localDt = GetTransitionDate(transition, year);
            return new DateTimeOffset(localDt - localOffset, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    internal static DateTime GetTransitionDate(TimeZoneInfo.TransitionTime transition, int year) =>
        transition.IsFixedDateRule
            ? new DateTime(year, transition.Month, transition.Day,
                transition.TimeOfDay.Hour, transition.TimeOfDay.Minute, transition.TimeOfDay.Second)
            : GetFloatingTransitionDate(year, transition);

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
}
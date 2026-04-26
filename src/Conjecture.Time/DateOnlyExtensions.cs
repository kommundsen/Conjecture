// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Time;

/// <summary>Extension methods on <see cref="Strategy{T}"/> for <see cref="DateOnly"/> test value generation.</summary>
public static class DateOnlyExtensions
{
    extension(Strategy<DateOnly> s)
    {
        /// <summary>
        /// Returns a strategy that generates dates that are the last or first day of any month.
        /// Generates a year (2000–2099), a month (1–12), then picks either day=1 or day=DaysInMonth(year,month).
        /// </summary>
        public Strategy<DateOnly> NearMonthBoundary()
        {
            Strategy<int> yearStrategy = Generate.Integers(2000, 2099);
            Strategy<int> monthStrategy = Generate.Integers(1, 12);
            Strategy<int> edgeStrategy = Generate.Integers(0, 1);

            return Generate.Compose<DateOnly>(ctx =>
            {
                int year = ctx.Generate(yearStrategy);
                int month = ctx.Generate(monthStrategy);
                int edge = ctx.Generate(edgeStrategy);
                int day = edge == 0 ? 1 : DateTime.DaysInMonth(year, month);
                return new DateOnly(year, month, day);
            });
        }

        /// <summary>
        /// Returns a strategy that generates dates within ±1 day of Feb 29 in leap years (1970–2400).
        /// Uses <c>ctx.Assume</c> to filter to leap years, matching the pattern of
        /// <see cref="DateTimeOffsetExtensions.NearLeapYear"/>.
        /// </summary>
        public Strategy<DateOnly> NearLeapDay()
        {
            Strategy<int> yearStrategy = Generate.Integers(1970, 2400);
            Strategy<int> offsetStrategy = Generate.Integers(-1, 1);

            return Generate.Compose<DateOnly>(ctx =>
            {
                int year = ctx.Generate(yearStrategy);
                ctx.Assume(DateTime.IsLeapYear(year));
                int offset = ctx.Generate(offsetStrategy);
                return new DateOnly(year, 2, 29).AddDays(offset);
            });
        }
    }
}
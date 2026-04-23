// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Time;

/// <summary>Extension methods on <see cref="Strategy{T}"/> for <see cref="TimeOnly"/> test value generation.</summary>
public static class TimeOnlyExtensions
{
    extension(Strategy<TimeOnly> s)
    {
        /// <summary>
        /// Returns a strategy that generates times within 30 seconds of midnight (00:00:00).
        /// Covers both [0, 30s] and [23:59:30, MaxValue].
        /// </summary>
        public Strategy<TimeOnly> NearMidnight()
        {
            long threshold = 30 * TimeSpan.TicksPerSecond;
            Strategy<TimeOnly> nearStart = Generate.TimeOnlyValues(TimeOnly.MinValue, new TimeOnly(threshold));
            Strategy<TimeOnly> nearEnd = Generate.TimeOnlyValues(new TimeOnly(TimeOnly.MaxValue.Ticks - threshold), TimeOnly.MaxValue);
            return Generate.OneOf(nearStart, nearEnd);
        }

        /// <summary>
        /// Returns a strategy that generates times within 30 seconds of noon (12:00:00).
        /// Range: [11:59:30, 12:00:30].
        /// </summary>
        public Strategy<TimeOnly> NearNoon()
        {
            long noonTicks = new TimeOnly(12, 0, 0).Ticks;
            long threshold = 30 * TimeSpan.TicksPerSecond;
            return Generate.TimeOnlyValues(new TimeOnly(noonTicks - threshold), new TimeOnly(noonTicks + threshold));
        }

        /// <summary>
        /// Returns a strategy that generates times within 30 seconds of end of day (23:59:59).
        /// Range: [23:59:29, TimeOnly.MaxValue].
        /// </summary>
        public Strategy<TimeOnly> NearEndOfDay()
        {
            long threshold = 30 * TimeSpan.TicksPerSecond;
            return Generate.TimeOnlyValues(new TimeOnly(TimeOnly.MaxValue.Ticks - threshold), TimeOnly.MaxValue);
        }
    }
}
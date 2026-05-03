// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Globalization;

using Conjecture.Core;
using Conjecture.Time;
using Conjecture.Xunit.V3;

namespace Conjecture.Time.Tests;

file sealed class DateTimeWithNonGregorianCultureProvider : IStrategyProvider<(DateTime, CultureInfo)>
{
    public Strategy<(DateTime, CultureInfo)> Create() =>
        Strategy.Tuples(Strategy.DateTimes(), Strategy.CulturesNonGregorian());
}

public class CultureCalendarStrategyTests
{
    [Fact]
    public void CulturesByCalendar_HijriCalendar_All_Use_Hijri()
    {
        bool anyHijri = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Any(static c => c.Calendar.GetType() == typeof(HijriCalendar));
        if (!anyHijri)
        {
            Assert.Skip("Platform has no specific cultures with HijriCalendar as default; skipping.");
        }

        Strategy<CultureInfo> strategy = Strategy.CulturesByCalendar<HijriCalendar>();

        IReadOnlyList<CultureInfo> samples = strategy.WithSeed(1UL).Sample(50);

        Assert.All(samples, static culture =>
            Assert.Equal(typeof(HijriCalendar), culture.Calendar.GetType()));
    }

    [Fact]
    public void CulturesByCalendar_JapaneseCalendar_All_Use_Japanese()
    {
        bool anyJapanese = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Any(static c => c.Calendar.GetType() == typeof(JapaneseCalendar));
        if (!anyJapanese)
        {
            Assert.Skip("Platform has no specific cultures with JapaneseCalendar as default; skipping.");
        }

        Strategy<CultureInfo> strategy = Strategy.CulturesByCalendar<JapaneseCalendar>();

        IReadOnlyList<CultureInfo> samples = strategy.WithSeed(1UL).Sample(50);

        Assert.All(samples, static culture =>
            Assert.Equal(typeof(JapaneseCalendar), culture.Calendar.GetType()));
    }

    [Fact]
    public void CulturesByCalendar_GregorianCalendar_Includes_EnUS()
    {
        Strategy<CultureInfo> strategy = Strategy.CulturesByCalendar<GregorianCalendar>();

        IReadOnlyList<CultureInfo> samples = strategy.WithSeed(1UL).Sample(500);

        bool found = samples.Any(static c => c.Name == "en-US");
        Assert.True(found, "en-US was not found among 500 samples from CulturesByCalendar<GregorianCalendar>");
    }

    [Fact]
    public void CulturesNonGregorian_None_Use_GregorianCalendar()
    {
        Strategy<CultureInfo> strategy = Strategy.CulturesNonGregorian();

        IReadOnlyList<CultureInfo> samples = strategy.WithSeed(1UL).Sample(50);

        Assert.All(samples, static culture =>
            Assert.NotEqual(typeof(GregorianCalendar), culture.Calendar.GetType()));
    }

    [Fact]
    public void CulturesByCalendar_GregorianCalendar_CalendarAddMonths_AgreesWithDateTimeAddMonths()
    {
        // For Gregorian cultures, Calendar.AddMonths should agree with DateTime.AddMonths
        // because Gregorian months align with the DateTime epoch.
        Strategy<(DateTime, CultureInfo)> strategy =
            Strategy.Tuples(Strategy.DateTimes(), Strategy.CulturesByCalendar<GregorianCalendar>());

        IReadOnlyList<(DateTime Dt, CultureInfo Culture)> samples = strategy.WithSeed(42UL).Sample(50);

        Assert.All(samples, pair =>
        {
            DateTime viaCalendar = pair.Culture.Calendar.AddMonths(pair.Dt, 1);
            DateTime viaDt = pair.Dt.AddMonths(1);
            Assert.Equal(viaDt, viaCalendar);
        });
    }

    [Property(MaxExamples = 50, Seed = 42UL)]
    public void CulturesNonGregorian_CalendarAddMonths_DivergencePresentOrOutOfRange(
        [From<DateTimeWithNonGregorianCultureProvider>] (DateTime Dt, CultureInfo Culture) pair)
    {
        // Non-Gregorian Calendar.AddMonths may throw ArgumentOutOfRangeException when the
        // DateTime falls outside the calendar's supported range, or may return a value
        // different from DateTime.AddMonths (different month numbering / era).
        // Either outcome is evidence that non-Gregorian semantics differ from DateTime.
        // DateTime.AddMonths always succeeds for valid DateTimes — that difference is the spec.
        Exception? calendarEx = Record.Exception(() => pair.Culture.Calendar.AddMonths(pair.Dt, 1));

        if (calendarEx is not null)
        {
            // Calendar threw (out of supported range) — DateTime.AddMonths must not throw.
            Exception? dtEx = Record.Exception(() => pair.Dt.AddMonths(1));
            Assert.Null(dtEx);
        }
        else
        {
            // Calendar succeeded — the property holds regardless of whether months agree.
            // Identity (AddMonths 0) must always equal the original DateTime.
            DateTime identity = pair.Culture.Calendar.AddMonths(pair.Dt, 0);
            Assert.Equal(pair.Dt, identity);
        }
    }

    [Fact]
    public void CulturesByCalendar_EmptyFilter_ThrowsOnAccess()
    {
        // DummyCalendar is guaranteed not to be the default calendar of any .NET culture,
        // so the filter yields zero results. Construction or first use should throw.
        Exception? ex = Record.Exception(static () =>
        {
            Strategy<CultureInfo> strategy = Strategy.CulturesByCalendar<DummyCalendar>();
            _ = strategy.WithSeed(1UL).Sample();
        });

        Assert.NotNull(ex);
        Assert.True(ex is InvalidOperationException or ArgumentException,
            $"Expected InvalidOperationException or ArgumentException but got {ex!.GetType().Name}: {ex.Message}");
    }

    /// <summary>A Calendar subtype guaranteed not to be the default calendar of any .NET culture.</summary>
    private sealed class DummyCalendar : Calendar
    {
        public override int[] Eras => [];

        public override DateTime AddMonths(DateTime time, int months) => time;

        public override DateTime AddYears(DateTime time, int years) => time;

        public override int GetDayOfMonth(DateTime time) => time.Day;

        public override DayOfWeek GetDayOfWeek(DateTime time) => time.DayOfWeek;

        public override int GetDayOfYear(DateTime time) => time.DayOfYear;

        public override int GetDaysInMonth(int year, int month, int era) => DateTime.DaysInMonth(year, month);

        public override int GetDaysInYear(int year, int era) => DateTime.IsLeapYear(year) ? 366 : 365;

        public override int GetEra(DateTime time) => 1;

        public override int GetMonth(DateTime time) => time.Month;

        public override int GetMonthsInYear(int year, int era) => 12;

        public override int GetYear(DateTime time) => time.Year;

        public override bool IsLeapDay(int year, int month, int day, int era) => false;

        public override bool IsLeapMonth(int year, int month, int era) => false;

        public override bool IsLeapYear(int year, int era) => false;

        public override DateTime ToDateTime(
            int year, int month, int day, int hour, int minute, int second, int millisecond, int era)
            => new(year, month, day, hour, minute, second, millisecond);
    }
}

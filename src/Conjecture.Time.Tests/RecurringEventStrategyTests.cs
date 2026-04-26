// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Time;

namespace Conjecture.Time.Tests;

public class RecurringEventStrategyTests
{
    [Fact]
    public void RecurringEvents_OccurrencesAreWithinWindow()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Strategy<RecurringEventSample> strategy = Generate.RecurringEvents(
            static current => current + TimeSpan.FromHours(1),
            zone,
            TimeSpan.FromHours(24));

        IReadOnlyList<RecurringEventSample> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, sample => Assert.All(sample.Occurrences, occ => Assert.True(
                    occ >= sample.WindowStart && occ <= sample.WindowEnd,
                    $"Occurrence {occ} is outside window [{sample.WindowStart}, {sample.WindowEnd}]")));
    }

    [Fact]
    public void RecurringEvents_OccurrencesAreMonotonicallyIncreasing()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Strategy<RecurringEventSample> strategy = Generate.RecurringEvents(
            static current => current + TimeSpan.FromHours(1),
            zone,
            TimeSpan.FromHours(24));

        IReadOnlyList<RecurringEventSample> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, sample =>
        {
            for (int i = 1; i < sample.Occurrences.Count; i++)
            {
                Assert.True(
                    sample.Occurrences[i] > sample.Occurrences[i - 1],
                    $"Occurrence at index {i} ({sample.Occurrences[i]}) is not after previous ({sample.Occurrences[i - 1]})");
            }
        });
    }

    [Fact]
    public void RecurringEvents_WithHourlySchedule_OccurrencesAreOneHourApart()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Strategy<RecurringEventSample> strategy = Generate.RecurringEvents(
            static current => current + TimeSpan.FromHours(1),
            zone,
            TimeSpan.FromHours(24));

        RecurringEventSample sample = DataGen.SampleOne(strategy, seed: 1UL);

        if (sample.Occurrences.Count < 2)
        {
            return;
        }

        for (int i = 1; i < sample.Occurrences.Count; i++)
        {
            TimeSpan gap = sample.Occurrences[i] - sample.Occurrences[i - 1];
            Assert.Equal(TimeSpan.FromHours(1), gap);
        }
    }

    [Fact]
    public void NearDstTransition_WindowOverlapsDstChange()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Strategy<RecurringEventSample> strategy = Generate.RecurringEvents(
                static current => current + TimeSpan.FromHours(1),
                zone,
                TimeSpan.FromHours(24))
            .NearDstTransition();

        IReadOnlyList<RecurringEventSample> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, sample =>
        {
            bool hasTransition = false;
            TimeZoneInfo.AdjustmentRule[] rules = sample.Zone.GetAdjustmentRules();
            foreach (TimeZoneInfo.AdjustmentRule rule in rules)
            {
                // Check spring-forward transition
                DateTimeOffset springForward = GetTransitionOffset(rule.DaylightTransitionStart, sample.WindowStart.Year, sample.Zone);
                if (springForward >= sample.WindowStart && springForward <= sample.WindowEnd)
                {
                    hasTransition = true;
                    break;
                }

                // Check fall-back transition
                DateTimeOffset fallBack = GetTransitionOffset(rule.DaylightTransitionEnd, sample.WindowStart.Year, sample.Zone);
                if (fallBack >= sample.WindowStart && fallBack <= sample.WindowEnd)
                {
                    hasTransition = true;
                    break;
                }
            }

            Assert.True(hasTransition, $"Window [{sample.WindowStart}, {sample.WindowEnd}] does not overlap any DST transition in zone '{sample.Zone.Id}'");
        });
    }

    [Fact]
    public void RecurringEvents_EmptyOccurrences_WhenNextOccurrenceAlwaysReturnsNull()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Strategy<RecurringEventSample> strategy = Generate.RecurringEvents(
            static _ => (DateTimeOffset?)null,
            zone,
            TimeSpan.FromHours(24));

        IReadOnlyList<RecurringEventSample> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, sample => Assert.Empty(sample.Occurrences));
    }

    private static DateTimeOffset GetTransitionOffset(
        TimeZoneInfo.TransitionTime transition,
        int year,
        TimeZoneInfo zone)
    {
        DateTime transitionDate = GetTransitionDate(transition, year);
        return new DateTimeOffset(transitionDate, zone.GetUtcOffset(transitionDate));
    }

    private static DateTime GetTransitionDate(TimeZoneInfo.TransitionTime transition, int year)
    {
        if (transition.IsFixedDateRule)
        {
            return new DateTime(year, transition.Month, transition.Day) + transition.TimeOfDay.TimeOfDay;
        }

        // Floating date: find the Nth occurrence of DayOfWeek in the month
        int startDay = transition.Week == 5 ? DateTime.DaysInMonth(year, transition.Month) : 1;
        DateTime candidate = new(year, transition.Month, startDay);
        int daysUntil = ((int)transition.DayOfWeek - (int)candidate.DayOfWeek + 7) % 7;
        candidate = candidate.AddDays(daysUntil);
        if (transition.Week < 5)
        {
            candidate = candidate.AddDays(7 * (transition.Week - 1));
        }
        else
        {
            // "Week 5" means last occurrence
            while (candidate.Month == transition.Month && candidate.AddDays(7).Month == transition.Month)
            {
                candidate = candidate.AddDays(7);
            }
        }
        return candidate + transition.TimeOfDay.TimeOfDay;
    }
}
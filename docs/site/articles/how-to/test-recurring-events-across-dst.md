# How to test recurring event schedules across DST

Recurring jobs (daily reports, billing cycles, cron tasks) fail silently across DST transitions: a job scheduled for 2:30 AM either skips that slot during spring-forward or fires twice during fall-back. `Strategy.RecurringEvents` lets you verify these invariants without a scheduling library dependency.

> [!NOTE]
> Requires the `Conjecture.Time` NuGet package.

## Define a recurrence delegate

`Strategy.RecurringEvents` accepts a `Func<DateTimeOffset, DateTimeOffset?> nextOccurrence` delegate. It must return the next firing time strictly after the given instant, or `null` to signal no further occurrences:

```csharp
using Conjecture.Core;
using Conjecture.Time;

static DateTimeOffset? NextDailyAt2Am(DateTimeOffset after)
{
    // Next 02:00 in UTC — replace with your scheduler's logic
    DateTimeOffset candidate = after.UtcDateTime.Date.AddDays(1).AddHours(2);
    return candidate > after ? candidate : candidate.AddDays(1);
}
```

> [!WARNING]
> `nextOccurrence` must always return a value strictly after its input. After 10,000 non-advancing calls `Strategy.RecurringEvents` throws `InvalidOperationException` as a safeguard against infinite loops.

## Write a DST-invariant property

`Strategy.RecurringEvents` generates a `RecurringEventSample` containing the window boundaries, all occurrences within it, the zone, and the delegate — ready for invariant assertions:

```csharp
[Property]
public bool DailyJob_FiringCountIsReasonable(int x)
{
    TimeZoneInfo zone = DataGen.SampleOne(Strategy.TimeZone(preferDst: true));
    RecurringEventSample sample = DataGen.SampleOne(
        Strategy.RecurringEvents(NextDailyAt2Am, zone, window: TimeSpan.FromDays(7)));

    // A daily job should fire 6–8 times in a 7-day window (DST may add or remove one)
    return sample.Occurrences.Count is >= 6 and <= 8;
}
```

## Bias toward DST transition windows

`.NearDstTransition()` on `Strategy<RecurringEventSample>` shifts the window start to just before a real DST transition in the zone — the most adversarial region for recurring events:

```csharp
[Property]
public bool DailyJob_FiresAtLeastOnceAcrossTransition(int x)
{
    TimeZoneInfo zone = DataGen.SampleOne(Strategy.TimeZone(preferDst: true));
    RecurringEventSample sample = DataGen.SampleOne(
        Strategy.RecurringEvents(NextDailyAt2Am, zone, window: TimeSpan.FromDays(2))
            .NearDstTransition());

    // Even if the spring-forward gap swallows exactly one 2 AM slot, the job must fire
    // at least once across a 2-day window around the transition
    return sample.Occurrences.Count >= 1;
}
```

## Integrate with NCrontab or other schedulers

Because the delegate is a plain `Func`, you can wrap any scheduling library:

```csharp
using NCrontab;

CrontabSchedule cron = CrontabSchedule.Parse("30 2 * * *"); // 02:30 daily

static DateTimeOffset? NextOccurrence(DateTimeOffset after) =>
    new DateTimeOffset?(cron.GetNextOccurrence(after.UtcDateTime));

[Property]
public bool CronJob_NoDoubleFireInFallBack(int x)
{
    TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    RecurringEventSample sample = DataGen.SampleOne(
        Strategy.RecurringEvents(NextOccurrence, eastern, window: TimeSpan.FromDays(1))
            .NearDstTransition());

    // No two occurrences within the same logical slot (less than 23 hours apart)
    for (int i = 1; i < sample.Occurrences.Count; i++)
    {
        if (sample.Occurrences[i] - sample.Occurrences[i - 1] < TimeSpan.FromHours(23))
        {
            return false;
        }
    }

    return true;
}
```

## See also

- [Reference: Time strategies](../reference/time-strategies.md)
- [How to test DST-sensitive code](test-dst-sensitive-code.md)
- [Explanation: Why time bugs are disproportionately dangerous](../explanation/time-bugs.md)

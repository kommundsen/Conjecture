# How to test DateOnly and TimeOnly boundary conditions

Month-end rollovers, leap days, and midnight/noon transitions are persistent sources of date arithmetic bugs. Conjecture's `Conjecture.Time` extensions bias generation toward these values so a regular property test reliably hits them.

> [!NOTE]
> Requires the `Conjecture.Time` NuGet package.

## DateOnly boundary extensions

Chain extensions after `Strategy.DateOnlyValues()`.

### `.NearMonthBoundary()`

Generates the first or last day of a random month (year 2000–2099). Targets off-by-one errors in month-spanning ranges:

```csharp
using Conjecture.Core;
using Conjecture.Time;

[Property]
public bool BillingCycle_HasCorrectDayCount(int x)
{
    DateOnly start = DataGen.SampleOne(Strategy.DateOnlyValues().NearMonthBoundary());
    int days = BillingCycle.DaysInPeriod(start, start.AddMonths(1));
    return days is >= 28 and <= 31;
}
```

### `.NearLeapDay()`

Generates dates within ±1 day of February 29 in a leap year (years 1970–2400). Targets leap-year-specific bugs:

```csharp
[Property]
public bool AgeCalculator_HandlesLeapBirthdays(int x)
{
    DateOnly birthday = DataGen.SampleOne(Strategy.DateOnlyValues().NearLeapDay());
    DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
    int age = AgeCalculator.ComputeAge(birthday, today);
    return age >= 0;
}
```

## TimeOnly boundary extensions

Chain extensions after `Strategy.TimeOnlyValues()`.

### `.NearMidnight()`

Generates times within 30 seconds of midnight, covering both ends of the day: `[00:00:00, 00:00:30]` and `[23:59:30, 23:59:59.9999999]`:

```csharp
[Property]
public bool Scheduler_HandlesNearMidnight(int x)
{
    TimeOnly trigger = DataGen.SampleOne(Strategy.TimeOnlyValues().NearMidnight());
    return Scheduler.IsValidTriggerTime(trigger);
}
```

### `.NearNoon()`

Generates times within 30 seconds of 12:00:00. Useful for systems that treat noon as a shift boundary or special aggregation point:

```csharp
[Property]
public bool ShiftHandover_AtNoon_IsComplete(int x)
{
    TimeOnly handover = DataGen.SampleOne(Strategy.TimeOnlyValues().NearNoon());
    ShiftHandoverResult result = ShiftManager.ComputeHandover(handover);
    return result is not null;
}
```

### `.NearEndOfDay()`

Generates times within 30 seconds of 23:59:59:

```csharp
[Property]
public bool DailyReport_IncludesEndOfDayEntries(int x)
{
    TimeOnly entryTime = DataGen.SampleOne(Strategy.TimeOnlyValues().NearEndOfDay());
    DateOnly date = DateOnly.FromDateTime(DateTime.UtcNow);
    DailyReport report = ReportBuilder.Build(date);
    return report.Includes(date, entryTime);
}
```

## See also

- [Reference: Time strategies](../reference/time-strategies.md)
- [Explanation: Why time bugs are disproportionately dangerous](../explanation/time-bugs.md)

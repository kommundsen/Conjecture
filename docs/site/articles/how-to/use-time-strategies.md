# How to test time-dependent code

Generate deterministic, boundary-focused time values with `Conjecture.Time`.

## Install

```bash
dotnet add package Conjecture.Time
```

## Generate boundary dates

Chain extension methods on `Generate.DateTimeOffsets()` to target problematic time regions:

```csharp
using Conjecture.Core;
using Conjecture.Time;

[Property]
public bool MidnightRollover_HandledCorrectly(int x)
{
    DateTimeOffset dt = DataGen.SampleOne(Generate.DateTimeOffsets().NearMidnight());
    return FormatDate(dt).Contains(dt.Year.ToString());
}
```

Available boundary extensions:

| Method | Targets |
|---|---|
| `.NearMidnight()` | +/-30 min of midnight UTC |
| `.NearLeapYear()` | +/-1 day of Feb 29 |
| `.NearEpoch()` | Unix epoch, Y2K38, min/max date |
| `.NearDstTransition(zone?)` | +/-1 hour of a DST transition |

## Test with clock skew

Generate a cluster of `FakeTimeProvider` instances with bounded skew for distributed system tests:

```csharp
[Property]
public bool LeaderElection_ConvergesUnderSkew(int x)
{
    FakeTimeProvider[] clocks = DataGen.SampleOne(
        TimeGenerate.ClockSet(nodeCount: 3, maxSkew: TimeSpan.FromSeconds(5)));

    Cluster cluster = new(clocks);
    cluster.RunElection();
    return cluster.HasSingleLeader;
}
```

## Use TimeProvider as a property parameter

`TimeProviderArbitrary` is an `[Arbitrary]`-decorated provider. Use it with `[From<T>]`:

```csharp
[Property]
public bool Scheduler_FiresOnTime([From<TimeProviderArbitrary>] TimeProvider clock)
{
    Scheduler scheduler = new(clock);
    scheduler.ScheduleAt(((FakeTimeProvider)clock).GetUtcNow() + TimeSpan.FromMinutes(5));
    ((FakeTimeProvider)clock).Advance(TimeSpan.FromMinutes(5));
    return scheduler.HasFired;
}
```

## See also

- [Reference: Time strategies](../reference/time-strategies.md) — full API surface
- [Reference: Strategies](../reference/strategies.md) — core `Generate.DateTimeOffsets()`, `Generate.TimeSpans()`, etc.

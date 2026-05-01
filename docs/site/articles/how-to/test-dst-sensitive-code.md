# How to test DST-sensitive code

DST transitions cause production incidents in recurring jobs (a 2:00 AM task that fires twice in autumn or not at all in spring), time-window queries, and any code that converts between UTC and local time.
Conjecture's time strategies let you reproduce these conditions deterministically.

> [!NOTE]
> Requires the `Conjecture.Time` NuGet package.

## Generate DST-heavy time zones

`Strategy.TimeZone(preferDst: true)` biases zone sampling toward zones with active DST rules:

```csharp
using Conjecture.Core;
using Conjecture.Time;

[Property]
public bool DailyJob_FiresExactlyOnce(int x)
{
    TimeZoneInfo zone = DataGen.SampleOne(Strategy.TimeZone(preferDst: true));
    DateTimeOffset trigger = DataGen.SampleOne(
        Strategy.DateTimeOffsets().NearDstTransition(zone));

    DailyJob job = new(zone);
    int fires = job.CountFirings(trigger, trigger.AddDays(1));
    return fires == 1;
}
```

## Target DST transition windows

`.NearDstTransition(zone?)` constrains values to within ±1 hour of a real DST transition in `zone` — the window where ambiguous and invalid local times occur:

```csharp
[Property]
public bool LocalTimeConversion_Roundtrips(int x)
{
    TimeZoneInfo zone = DataGen.SampleOne(Strategy.TimeZone(preferDst: true));
    DateTimeOffset utc = DataGen.SampleOne(
        Strategy.DateTimeOffsets().NearDstTransition(zone));

    DateTimeOffset local = TimeZoneInfo.ConvertTime(utc, zone);
    DateTimeOffset backToUtc = TimeZoneInfo.ConvertTimeToUtc(local.DateTime, zone);
    return backToUtc == utc;
}
```

If `zone` is `null`, a random DST-having zone from the system list is chosen automatically.

## Use IANA zone IDs for cross-platform tests

`Strategy.IanaZoneIds()` samples from a curated ~20-ID cross-platform-safe subset verified to resolve on .NET 8+ on Windows, Linux, and macOS:

```csharp
[Property]
public bool ZoneConversion_WorksAcrossPlatforms(int x)
{
    string ianaId = DataGen.SampleOne(Strategy.IanaZoneIds());
    TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
    return zone is not null;
}
```

For Windows timezone IDs, use `Strategy.WindowsZoneIds()`. For DST-heavy IDs specifically, pass `preferDst: true`:

```csharp
string dstIanaId = DataGen.SampleOne(Strategy.IanaZoneIds(preferDst: true));
```

> [!NOTE]
> The curated IANA subset covers common enterprise zones. For exhaustive coverage, use `Strategy.TimeZones()` which calls `TimeZoneInfo.GetSystemTimeZones()`, but note that available IDs differ across operating systems.

## See also

- [Reference: Time strategies](../reference/time-strategies.md) — full API details
- [How to test recurring event schedules across DST](test-recurring-events-across-dst.md)
- [Explanation: Why time bugs are disproportionately dangerous](../explanation/time-bugs.md)

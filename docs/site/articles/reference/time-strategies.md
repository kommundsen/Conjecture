# Time strategies reference

Strategies in the `Conjecture.Time` package for generating time-related values with boundary awareness.

> [!NOTE]
> Requires the `Conjecture.Time` NuGet package. The core `Generate.DateTimeOffsets()`, `Generate.TimeSpans()`, `Generate.DateOnlyValues()`, `Generate.TimeOnlyValues()`, and `Generate.DateTimes()` methods are in `Conjecture.Core` and do not require this package.

---

## TimeZoneInfo strategies

### `Generate.TimeZones()`

```csharp
Strategy<TimeZoneInfo> Generate.TimeZones()
```

Picks uniformly from all system time zones via `TimeZoneInfo.GetSystemTimeZones()`. Shrinks toward `TimeZoneInfo.Utc`.

> [!NOTE]
> Available IDs differ across operating systems. For reproducible cross-platform tests, prefer `Generate.TimeZone()` or `Generate.IanaZoneIds()`.

### `Generate.TimeZone(bool preferDst = false)`

```csharp
Strategy<TimeZoneInfo> Generate.TimeZone(bool preferDst = false)
```

Samples from a curated ~20-ID cross-platform-safe subset verified to resolve on .NET 8+ on Windows, Linux, and macOS. When `preferDst` is `true`, restricts to the subset of zones with active DST rules.

### `Generate.IanaZoneIds(bool preferDst = false)`

```csharp
Strategy<string> Generate.IanaZoneIds(bool preferDst = false)
```

Returns IANA timezone IDs from the same cross-platform-safe curated subset as `Generate.TimeZone()`. Pass `preferDst: true` to restrict to DST-having zones. Use the returned string with `TimeZoneInfo.FindSystemTimeZoneById(id)`.

### `Generate.WindowsZoneIds()`

```csharp
Strategy<string> Generate.WindowsZoneIds()
```

Returns Windows timezone IDs derived from the curated IANA subset via `TimeZoneInfo.TryConvertIanaIdToWindowsId`. Useful for testing code that stores or reads Windows-format zone IDs.

---

## FakeTimeProvider strategies

### `Generate.ClockSet(int nodeCount, TimeSpan maxSkew)`

```csharp
Strategy<FakeTimeProvider[]> Generate.ClockSet(int nodeCount, TimeSpan maxSkew)
```

Generates an array of `nodeCount` `FakeTimeProvider` instances, each with a clock offset in `[-maxSkew/2, +maxSkew/2]` relative to `DateTimeOffset.UtcNow`. `nodeCount` must be >= 2.

Use for distributed-system tests where nodes observe slightly different wall-clock times.

### `Generate.AdvancingClocks(TimeSpan maxJump)`

```csharp
Strategy<FakeTimeProvider> Generate.AdvancingClocks(TimeSpan maxJump)
```

Generates a single `FakeTimeProvider` pre-positioned at a random time within `[2000-01-01, 2000-01-01 + maxJump]`. `maxJump` must be positive.

### `Generate.ClockWithAdvances(int advanceCount, TimeSpan maxJump, bool allowBackward = false)`

```csharp
Strategy<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)>
    Generate.ClockWithAdvances(int advanceCount, TimeSpan maxJump, bool allowBackward = false)
```

Generates a `FakeTimeProvider` paired with `advanceCount` pre-drawn `TimeSpan` advances, each in `[0, maxJump]` (or `[-maxJump, maxJump]` when `allowBackward` is `true`). `advanceCount` must be >= 1.

Use to drive the clock through adversarial sequences without coupling the test to `Thread.Sleep`:

```csharp
[Property]
public bool TokenRefresh_HandlesClockJumps(int x)
{
    (FakeTimeProvider clock, IReadOnlyList<TimeSpan> advances) = DataGen.SampleOne(
        Generate.ClockWithAdvances(advanceCount: 5, maxJump: TimeSpan.FromMinutes(10)));

    TokenService svc = new(clock);
    foreach (TimeSpan advance in advances)
    {
        clock.Advance(advance);
        svc.RefreshIfNeeded();
    }

    return svc.Token.IsValid;
}
```

### `TimeProviderArbitrary`

```csharp
[Arbitrary]
public sealed class TimeProviderArbitrary : IStrategyProvider<TimeProvider>
```

Auto-provider for `TimeProvider` parameters. Each generated value is a `FakeTimeProvider` with a deterministic epoch start. Use with `[From<TimeProviderArbitrary>]` or let `[Arbitrary]` resolution pick it up automatically.

---

## Recurring event strategies

### `Generate.RecurringEvents(...)`

```csharp
Strategy<RecurringEventSample> Generate.RecurringEvents(
    Func<DateTimeOffset, DateTimeOffset?> nextOccurrence,
    TimeZoneInfo zone,
    TimeSpan window)
```

Generates a `RecurringEventSample` by walking `nextOccurrence` from a random window start until `window` elapses. `nextOccurrence` must return a value strictly after its input; returning `null` signals no further occurrences. Throws `InvalidOperationException` after 10,000 non-advancing steps.

### `RecurringEventSample` record

```csharp
public sealed record RecurringEventSample(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    IReadOnlyList<DateTimeOffset> Occurrences,
    TimeZoneInfo Zone,
    Func<DateTimeOffset, DateTimeOffset?> NextOccurrence);
```

### `.NearDstTransition()` on `Strategy<RecurringEventSample>`

```csharp
Strategy<RecurringEventSample> NearDstTransition()
```

Biases the window start to just before a DST transition in `RecurringEventSample.Zone`. Re-walks `NextOccurrence` so `Occurrences` reflects the shifted window. Falls back to the base strategy when no transitions are available.

---

## DateTimeOffset extension methods

Chain after `Generate.DateTimeOffsets()`. These methods are in `Conjecture.Core` except where noted.

| Method | Package | Description |
|---|---|---|
| `.NearMidnight()` | Core | ±30 min of midnight UTC |
| `.NearLeapYear()` | Core | ±1 day of Feb 29 in a leap year |
| `.NearEpoch()` | Core | ±1 hour of Unix epoch, min/max, Y2K38 |
| `.NearDstTransition(TimeZoneInfo? zone)` | `Conjecture.Time` | ±1 hour of a DST transition |
| `.WithPrecision(TimeSpan precision)` | `Conjecture.Time` | Truncates to provider precision |
| `.WithStrippedOffset()` | `Conjecture.Time` | Pairs with offset-stripped (UTC) copy |

### `.NearDstTransition(TimeZoneInfo? zone = null)`

```csharp
Strategy<DateTimeOffset> NearDstTransition(TimeZoneInfo? zone = null)
```

Values within ±1 hour of a DST transition in `zone`. Picks a random DST-having system zone if `zone` is `null`. Falls back to `.NearEpoch()` if no transitions are found.

### `.WithPrecision(TimeSpan precision)`

```csharp
Strategy<DateTimeOffset> WithPrecision(TimeSpan precision)
```

Truncates each value to `precision` ticks, matching provider-imposed precision. `precision` must be positive.

### `.WithStrippedOffset()`

```csharp
Strategy<(DateTimeOffset Original, DateTimeOffset Stripped)> WithStrippedOffset()
```

Pairs each value with a copy that has the UTC offset removed (`Offset = Zero`), modelling providers that lose the offset on roundtrip.

---

## DateOnly extension methods

Chain after `Generate.DateOnlyValues()`.

### `.NearMonthBoundary()`

```csharp
Strategy<DateOnly> NearMonthBoundary()
```

Generates the first or last day of a random month (years 2000–2099).

### `.NearLeapDay()`

```csharp
Strategy<DateOnly> NearLeapDay()
```

Generates dates within ±1 day of February 29 in a leap year (years 1970–2400). Uses `ctx.Assume` to filter to leap years.

---

## TimeOnly extension methods

Chain after `Generate.TimeOnlyValues()`.

### `.NearMidnight()`

```csharp
Strategy<TimeOnly> NearMidnight()
```

Generates times within 30 seconds of midnight, covering both ends: `[00:00:00, 00:00:30]` and `[23:59:30, TimeOnly.MaxValue]`.

### `.NearNoon()`

```csharp
Strategy<TimeOnly> NearNoon()
```

Generates times within 30 seconds of 12:00:00, in the range `[11:59:30, 12:00:30]`.

### `.NearEndOfDay()`

```csharp
Strategy<TimeOnly> NearEndOfDay()
```

Generates times within 30 seconds of end of day: `[23:59:30, TimeOnly.MaxValue]`.

---

## DateTime extension methods

Chain after `Generate.DateTimes()`.

### `.WithKinds()`

```csharp
Strategy<(DateTime Value, DateTimeKind Kind)> WithKinds()
```

Pairs each generated `DateTime` with a randomly chosen `DateTimeKind` (`Utc`, `Local`, `Unspecified`), covering all three uniformly. Shrinks toward `Utc`.

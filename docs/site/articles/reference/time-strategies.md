# Time strategies reference

Strategies in the `Conjecture.Time` package for generating time-related values with boundary awareness.

> [!NOTE]
> Requires the `Conjecture.Time` NuGet package. The core `Generate.DateTimeOffsets()`, `Generate.TimeSpans()`, `Generate.DateOnlyValues()`, and `Generate.TimeOnlyValues()` methods are in `Conjecture.Core` and do not require this package.

## `Generate.TimeZones()`

```csharp
Strategy<TimeZoneInfo> Generate.TimeZones()
```

Picks uniformly from system time zones. Shrinks toward `TimeZoneInfo.Utc`.

## `Generate.ClockSet(int nodeCount, TimeSpan maxSkew)`

```csharp
Strategy<FakeTimeProvider[]> Generate.ClockSet(int nodeCount, TimeSpan maxSkew)
```

Generates an array of `nodeCount` `FakeTimeProvider` instances, each with a clock offset in `[-maxSkew/2, +maxSkew/2]` relative to `DateTimeOffset.UtcNow`. `nodeCount` must be >= 2.

## `TimeProviderArbitrary`

```csharp
[Arbitrary]
public sealed class TimeProviderArbitrary : IStrategyProvider<TimeProvider>
```

Auto-provider for `TimeProvider` parameters. Each generated value is a `FakeTimeProvider` with a deterministic epoch start.

Use with `[From<TimeProviderArbitrary>]` or let `[Arbitrary]` resolution pick it up automatically.

## DateTimeOffset extension methods

Extension properties on `Strategy<DateTimeOffset>` that narrow the output to values near interesting temporal boundaries. Chain after `Generate.DateTimeOffsets()`.

### `.NearMidnight()`

```csharp
Strategy<DateTimeOffset> NearMidnight()
```

Values within +/-30 minutes of midnight UTC.

### `.NearLeapYear()`

```csharp
Strategy<DateTimeOffset> NearLeapYear()
```

Values within +/-1 day of February 29 in a leap year (years 1970-2400).

### `.NearEpoch()`

```csharp
Strategy<DateTimeOffset> NearEpoch()
```

Values within +/-1 hour of well-known epoch anchors: Unix epoch (1970-01-01), near-min (0001-01-02), near-max (9999-12-30), Y2K38 (2038-01-19).

### `.NearDstTransition(TimeZoneInfo? zone = null)`

```csharp
Strategy<DateTimeOffset> NearDstTransition(TimeZoneInfo? zone = null)
```

Values within +/-1 hour of a DST transition. Picks a random DST-having zone if `zone` is null. Falls back to `.NearEpoch()` if no transitions are found.

# Draft: TimeProvider Integration

## Motivation

Time-dependent logic is notoriously hard to test: caches expire, tokens time out, rate limiters reset, schedulers fire, and retries back off — all governed by the clock. .NET 8 introduced `TimeProvider` as a first-class abstraction for time, and .NET 10 applications increasingly adopt it. Conjecture can generate time sequences and control clock progression during property execution, turning temporal edge cases (leap seconds, DST transitions, clock skew, exact-expiry boundaries) from "discovered in production" into "caught by a property test."

## .NET Advantage

`TimeProvider` (in `System.Runtime`, in-box since .NET 8) and `FakeTimeProvider` (in `Microsoft.Extensions.TimeProvider.Testing`) provide the seams needed for deterministic time control. Conjecture can:
- Replace the ambient `TimeProvider` during property execution
- Generate `DateTimeOffset` sequences that hit interesting boundaries
- Advance time between property steps to test temporal transitions
- All without `Thread.Sleep` or wall-clock dependencies

## Key Ideas

### Time Strategy Primitives
```csharp
// Generate interesting timestamps
Generate.DateTimeOffsets()                          // full range
Generate.DateTimeOffsets(min, max)                  // bounded
Generate.DateTimeOffsets().NearDstTransition()      // DST boundaries
Generate.DateTimeOffsets().NearMidnight()            // day boundaries
Generate.DateTimeOffsets().NearLeapSecond()          // leap second adjacency
Generate.DateTimeOffsets().NearEpoch()               // Unix epoch boundaries

// Generate durations
Generate.TimeSpans(min, max)
Generate.TimeSpans().SubSecond()                    // for timeout edge cases
Generate.TimeSpans().HumanScale()                   // seconds to hours

// Generate time zones
Generate.TimeZones()                                // from system time zones
```

### Controlled Clock in Properties
```csharp
[Property]
public bool CacheExpiresCorrectly(
    TimeSpan ttl,
    [FromStrategy(nameof(SmallDelays))] TimeSpan[] delays)
{
    var clock = new FakeTimeProvider(startTime: DateTimeOffset.UtcNow);
    var cache = new ExpiringCache<string, int>(clock, ttl);

    cache.Set("key", 42);

    foreach (var delay in delays)
    {
        clock.Advance(delay);
        var exists = cache.TryGet("key", out _);

        if (clock.GetUtcNow() - startTime >= ttl)
            Assert.False(exists, "Cache should have expired");
        else
            Assert.True(exists, "Cache should still be valid");
    }

    return true;
}
```

### Time Sequence Generation
```csharp
// Generate monotonically increasing timestamps
Generate.TimeSequence(
    start: DateTimeOffset.UtcNow,
    stepStrategy: Generate.TimeSpans(TimeSpan.Zero, TimeSpan.FromHours(1)),
    count: Generate.Integers<int>(1, 100)
)
// Result: [t0, t0+3min, t0+47min, t0+48min, ...]
```

### Clock Skew and Distributed Time
```csharp
// Generate multiple clocks with bounded skew (for distributed systems)
Generate.ClockSet(
    nodeCount: 3,
    maxSkew: TimeSpan.FromSeconds(5)
)
// Result: 3 FakeTimeProviders, each offset by a generated skew
```

### Temporal Shrinking
- Shrink timestamps toward epoch (simpler values)
- Shrink durations toward zero
- Shrink time sequences toward fewer steps with shorter intervals
- Preserve temporal ordering during shrinking

### Interesting Time Boundaries
Built-in awareness of:
- DST transitions (spring forward, fall back)
- Leap years (Feb 29)
- Year boundaries (Dec 31 → Jan 1)
- Unix epoch (1970-01-01)
- .NET epoch limits (`DateTimeOffset.MinValue`, `.MaxValue`)
- 32-bit Unix time overflow (2038-01-19)
- Midnight boundaries
- Time zone transitions for common zones

## Design Decisions to Make

1. Ship in `Conjecture.Core` or a separate `Conjecture.Time` package?
2. Should `[Property]` auto-inject a `FakeTimeProvider` when a parameter of type `TimeProvider` is present?
3. How to handle `FakeTimeProvider` dependency — it's in `Microsoft.Extensions.TimeProvider.Testing` (not in-box). Require it or provide our own minimal fake?
4. Should time strategies be aware of the system's time zone database? (Adds platform dependency)
5. How deep should DST/leap-second awareness go? (IANA database or simplified heuristics?)
6. Should clock advancement be integrated with stateful testing? (Advance clock between state machine steps)

## Scope Estimate

Small-Medium. Core time strategies are ~1 cycle. Clock injection and temporal shrinking add ~1 more. DST/boundary awareness is ~1 more.

## Dependencies

- `System.TimeProvider` (in-box since .NET 8)
- `Microsoft.Extensions.TimeProvider.Testing` for `FakeTimeProvider` (optional dependency)
- `TimeZoneInfo` (in-box) for time zone data
- Existing strategy composition infrastructure

## Open Questions

- How common is `TimeProvider` adoption in .NET 10 codebases? (Determines immediate value)
- Should we support `ISystemClock` (the older ASP.NET Core abstraction) for backward compat?
- How to test Conjecture's own time strategies? (Time-testing a time-testing tool is recursive)
- Should the Aspire integration draft use this for testing time-dependent distributed interactions?
- Is there value in generating `NodaTime` types for users of that library?
- How to handle timer-based code (`PeriodicTimer`, `Task.Delay`) — can we intercept these via `FakeTimeProvider`?

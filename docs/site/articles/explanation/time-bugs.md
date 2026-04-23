# Why time bugs are disproportionately dangerous

Time-related bugs are among the most expensive in enterprise .NET. They are rare enough that teams underinvest in testing them, subtle enough that they survive code review, and devastating when they occur — corrupting billing records, misfiring scheduled tasks, or creating silent data loss that takes days to diagnose. This page explains the four mechanisms that make time bugs so difficult to catch.

## DST transitions create invalid and ambiguous times

Twice a year, most time zones perform a Daylight Saving Time (DST) adjustment:

- **Spring-forward**: clocks skip from 02:00 to 03:00. The times 02:00–02:59 do not exist in that zone on that day. Any code that constructs or maps a local time in this gap either throws, silently wraps, or produces an incorrect result.
- **Fall-back**: clocks repeat 01:00–01:59. Every local time in that window is ambiguous — it could be the first or second occurrence. Code that does not explicitly handle this maps to the wrong UTC instant roughly half the time.

The impact is concrete: a nightly billing job scheduled for 02:30 local time will skip its execution during spring-forward and run twice during fall-back. A cron-based reminder will fire at the wrong UTC time for all users in the affected zone.

Standard unit tests almost never cover this because developers write tests against `DateTime.UtcNow` and specific dates, not against the ±1-hour window around the transition. Conjecture's `.NearDstTransition()` and `Generate.TimeZone(preferDst: true)` target exactly this region.

## `DateTime.Kind` silently redefines semantics

`DateTime.Kind` is a three-valued property — `Utc`, `Local`, `Unspecified` — that .NET uses to interpret the value. The same ticks with a different `Kind` represent a different instant, but the property is invisible in most logs, JSON output, and database records:

- Serialisers that assume `Kind == Utc` will emit the wrong timestamp when given a `Local` or `Unspecified` value.
- ORMs that coerce `Unspecified` to `Utc` silently shift timestamps by the server's UTC offset.
- `DateTime.Compare` and `==` ignore `Kind`, so tests written with `Assert.Equal` pass even when the stored value has the wrong kind.

The `Unspecified` kind is the most dangerous: it is the default when parsing without explicit format information and is also the value returned by many date arithmetic operations. Teams discover these bugs months later when a server is moved to a different time zone.

`Generate.DateTimes().WithKinds()` generates all three kinds in a single property, making it impossible to accidentally test only `Utc`.

## Database providers strip or round time values

The gap between what .NET stores in a `DateTimeOffset` and what the database returns is a persistent source of bugs:

| Scenario | What happens |
|---|---|
| SQL Server `datetime2(3)` | Rounds sub-millisecond ticks; your 100 ns precision is silently lost |
| Most providers (EF Core default) | Strips the UTC offset; reconstructed value is always `Offset = Zero` |
| SQLite text storage | Rounds to seconds |
| PostgreSQL `timestamp` (no tz) | Drops offset; value stored as naive local time |

The bug manifests as a roundtrip inequality: `original != db.Reload().Timestamp`. Because this inequality only involves sub-millisecond precision or the offset value, it is invisible to any test that asserts only on the date and time components.

`.WithPrecision(TimeSpan precision)` lets you generate values that already have the precision the provider will store, making the roundtrip property deterministically true or false. `.WithStrippedOffset()` models offset-dropping providers explicitly.

## Clock skew breaks distributed invariants

In distributed systems, each node's `DateTimeOffset.UtcNow` is not identical. NTP keeps clocks roughly synchronised, but "roughly" means differences of tens to hundreds of milliseconds under normal conditions, and arbitrary differences during failover or leap-second events. Bugs that depend on this:

- Leader elections that assume the leader's clock is always ahead of followers.
- Event-sourcing systems that use `UtcNow` as a sequence number and fail when events arrive out of order.
- Token expiry checks that rely on clock monotonicity.
- Rate limiters that bucket requests by minute and silently allow extra requests at minute boundaries.

These bugs are untestable with `DateTime.UtcNow` because the test process has a single clock. `Generate.ClockSet(nodeCount, maxSkew)` generates a cluster of `FakeTimeProvider` instances with bounded pairwise skew, and `Generate.ClockWithAdvances(advanceCount, maxJump, allowBackward: true)` generates adversarial clock sequences including backward jumps.

## Historical note on scope

Conjecture's time strategies target the BCL types: `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeZoneInfo`, `DateTimeKind`. Historical timezone data (pre-1970 DST rule changes, the 1883 US railroad standardisation, the 2011 Samoa date line change) is not in scope — the strategies use the current OS tzdata, which does not include historical adjustments. For historically accurate coverage, use the future NodaTime adapter.

## See also

- [How to test DST-sensitive code](../how-to/test-dst-sensitive-code.md)
- [How to test DateTime.Kind safety](../how-to/test-datetime-kind-safety.md)
- [How to test DateOnly/TimeOnly boundary conditions](../how-to/test-dateonly-timeonly-boundaries.md)
- [How to test DB provider DateTimeOffset precision](../how-to/test-datetimeoffset-precision.md)
- [How to test recurring event schedules across DST](../how-to/test-recurring-events-across-dst.md)
- [Reference: Time strategies](../reference/time-strategies.md)

# How to test DB provider DateTimeOffset precision

Database providers commonly transform `DateTimeOffset` values on write: SQL Server rounds to 100 ns, many providers strip the UTC offset entirely and store UTC. These silent transformations produce bugs that only appear in integration tests with real data round-tripped through the database.

> [!NOTE]
> Requires the `Conjecture.Time` NuGet package.

## Test millisecond-precision roundtrips

`.WithPrecision(TimeSpan precision)` truncates each generated value to the given precision, matching what the provider actually stores, so you can assert that the value survives a full roundtrip:

```csharp
using Conjecture.Core;
using Conjecture.Time;

[Property]
public bool Repository_RoundtripsDateTimeOffset(int x)
{
    DateTimeOffset value = DataGen.SampleOne(
        Strategy.DateTimeOffsets().WithPrecision(TimeSpan.FromMilliseconds(1)));

    using TestDbContext db = new();
    db.Orders.Add(new Order { PlacedAt = value });
    db.SaveChanges();
    db.ChangeTracker.Clear();

    DateTimeOffset stored = db.Orders.Single().PlacedAt;
    return stored == value;
}
```

> [!TIP]
> Match precision to your provider:
> - SQL Server `datetime2(7)` → `TimeSpan.FromTicks(1)` (100 ns)
> - SQL Server `datetime2(3)` / most others → `TimeSpan.FromMilliseconds(1)`
> - SQLite → `TimeSpan.FromSeconds(1)` (text storage, second precision by default)

## Test offset-stripping roundtrips

`.WithStrippedOffset()` returns each value paired with a UTC-normalised copy (offset removed), modelling providers that store UTC and reconstruct the value without the original offset:

```csharp
[Property]
public bool AuditLog_PreservesUtcTimeAfterNormalisation(int x)
{
    (DateTimeOffset original, DateTimeOffset stripped) = DataGen.SampleOne(
        Strategy.DateTimeOffsets().WithStrippedOffset());

    // stripped == original converted to UTC (Offset = Zero, Ticks unchanged)
    AuditEntry entry = AuditLog.Record(original);
    AuditEntry loaded = AuditLog.Load(entry.Id);

    return loaded.Timestamp.ToUniversalTime() == original.ToUniversalTime();
}
```

## Test near DST transitions

`.NearDstTransition(zone?)` generates values within ±1 hour of a DST transition, which is when offset-stripping bugs are most likely to cause incorrect local-time reconstruction:

```csharp
[Property]
public bool Cache_ReconstructsCorrectLocalTime_AroundDst(int x)
{
    TimeZoneInfo zone = DataGen.SampleOne(Strategy.TimeZone(preferDst: true));
    DateTimeOffset value = DataGen.SampleOne(
        Strategy.DateTimeOffsets().NearDstTransition(zone));

    CacheEntry cached = Cache.Store(value, zone);
    DateTimeOffset reconstructed = Cache.Load(cached.Key, zone);

    return reconstructed.ToUniversalTime() == value.ToUniversalTime();
}
```

## See also

- [Reference: Time strategies](../reference/time-strategies.md)
- [How to test DST-sensitive code](test-dst-sensitive-code.md)
- [Explanation: Why time bugs are disproportionately dangerous](../explanation/time-bugs.md)

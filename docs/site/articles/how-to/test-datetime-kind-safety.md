# How to test DateTime.Kind safety

`DateTime.Kind` silently changes how a value is interpreted. ORMs, JSON serialisers, and HTTP clients that assume `Kind == Utc` produce incorrect results when passed a `Local` or `Unspecified` value — and these bugs are invisible at the call site.

> [!NOTE]
> Requires the `Conjecture.Time` NuGet package.

## Fuzz all three kinds

`.WithKinds()` generates each `DateTime` value paired with a randomly chosen `DateTimeKind`, covering `Utc`, `Local`, and `Unspecified` uniformly:

```csharp
using Conjecture.Core;
using Conjecture.Time;

[Property]
public bool Serializer_PreservesKind(int x)
{
    (DateTime value, DateTimeKind kind) = DataGen.SampleOne(
        Generate.DateTimes().WithKinds());

    string json = JsonSerializer.Serialize(new { Timestamp = value });
    DateTime roundtripped = JsonSerializer.Deserialize<TimestampDto>(json)!.Timestamp;
    return roundtripped.Kind == kind;
}
```

The strategy shrinks toward `Kind == Utc` and the earliest dates, so a failure simplifies to the minimal `Kind` variant that triggers the bug.

## Catch silent Kind coercion

Many ORMs coerce `Unspecified` to `Utc` without warning. A property that exposes this:

```csharp
[Property]
public bool DbContext_DoesNotSilentlyCoerceKind(int x)
{
    (DateTime value, DateTimeKind kind) = DataGen.SampleOne(
        Generate.DateTimes().WithKinds());

    using TestDbContext db = new();
    db.Events.Add(new Event { OccurredAt = value });
    db.SaveChanges();
    db.ChangeTracker.Clear();

    DateTime stored = db.Events.Single().OccurredAt;
    return stored.Kind == kind;
}
```

When this fails, Conjecture will shrink to the minimal `DateTimeKind` and simplest date that exposes the coercion.

> [!TIP]
> If you only care about one kind, filter with `Where`:
> ```csharp
> Generate.DateTimes().WithKinds()
>     .Where(static t => t.Kind == DateTimeKind.Unspecified)
> ```

## See also

- [Reference: Time strategies](../reference/time-strategies.md)
- [Explanation: Why time bugs are disproportionately dangerous](../explanation/time-bugs.md)

# Conjecture.EFCore reference

API reference for the `Conjecture.EFCore` package. Install via:

```xml
<PackageReference Include="Conjecture.EFCore" />
```

`Conjecture.EFCore` derives entity-graph strategies from an EF Core `IModel`, asserts `SaveChanges` roundtrips without data loss, and verifies migration up/down symmetry. v1 ships with SQLite (in-memory) as the canonical test provider; any provider that produces a valid `IModel` works for entity strategies. The migration harness is SQLite-only in v1.

> [!NOTE]
> Design rationale lives in [ADR 0065: Conjecture.EFCore package design](../../decisions/0065-conjecture-efcore-package-design.md).

---

## `Generate.Entity<T>` — extension methods on `Generate`

```csharp
namespace Conjecture.EFCore;

public static class EFCoreGenerateExtensions
{
    extension(Conjecture.Core.Generate)
    {
        public static Strategy<T> Entity<T>(DbContext context, int maxDepth = 2) where T : class;
        public static Strategy<T> Entity<T>(Func<DbContext> contextFactory, int maxDepth = 2) where T : class;
    }
}
```

| Overload | Use when |
|---|---|
| `Entity<T>(DbContext context, int maxDepth = 2)` | You already hold a `DbContext` instance — typically inside a single test method or a class fixture. The strategy reads `context.Model` once on construction. |
| `Entity<T>(Func<DbContext> contextFactory, int maxDepth = 2)` | You want each draw to build the entity in a fresh `DbContext`, e.g. when seeding a test that itself creates and disposes a context per example. |

`maxDepth` controls navigation-property recursion (default `2`). Required reference navigations beyond the bound are populated by reusing the first generated parent of the same target type; optional reference navigations are set to `null`; collection navigations are emitted empty. Owned types are always generated inline regardless of `maxDepth`.

Both overloads delegate to [`EntityStrategyBuilder`](#entitystrategybuilder) — drop down to the builder when you need `WithoutNavigation` or other model-level customisation.

```csharp
using Conjecture.Core;
using Conjecture.EFCore;

await using AppDbContext db = CreateContext();
Strategy<Order> orders = Generate.Entity<Order>(db);
Order example = orders.Sample();
```

---

## `EntityStrategyBuilder`

```csharp
namespace Conjecture.EFCore;

public sealed class EntityStrategyBuilder
{
    public EntityStrategyBuilder(IModel model);
    public EntityStrategyBuilder WithMaxDepth(int depth);
    public EntityStrategyBuilder WithoutNavigation<TEntity>(Expression<Func<TEntity, object?>> navigation);
    public Strategy<TEntity> Build<TEntity>() where TEntity : class;
}
```

Builder-style entry point for entity strategies. Use this when you need to suppress specific navigations or set a custom recursion bound, then call `Build<TEntity>()` once per entity type.

| Member | Purpose |
|---|---|
| `EntityStrategyBuilder(IModel model)` | Construct from `context.Model`. The builder caches per-entity strategies on first build. |
| `WithMaxDepth(int depth)` | Override navigation-cycle bound. Default `2`. Pass `0` to suppress navigations entirely. |
| `WithoutNavigation<TEntity>(expr)` | Drop a specific navigation by lambda accessor: `b.WithoutNavigation<Customer>(c => c.Orders)`. Reference navigations become `null`; collection navigations become empty. |
| `Build<TEntity>()` | Returns `Strategy<TEntity>` for an entity type registered in the model. Throws `InvalidOperationException` for unknown types. |

Mutable-self builder — `WithMaxDepth` and `WithoutNavigation` return `this`.

```csharp
EntityStrategyBuilder b = new EntityStrategyBuilder(db.Model)
    .WithMaxDepth(1)
    .WithoutNavigation<Customer>(c => c.Orders);

Strategy<Customer> customers = b.Build<Customer>();
```

---

## `PropertyStrategyBuilder`

```csharp
namespace Conjecture.EFCore;

public static class PropertyStrategyBuilder
{
    public static Strategy<object?> Build(IProperty property);
}
```

Maps a single EF Core `IProperty` to a `Strategy<object?>` (boxed) honouring the v1 type-system constraint scope. Used internally by `EntityStrategyBuilder`; exposed for callers who want to plug primitive strategies into a hand-written entity composer.

| Honoured | Mechanism |
|---|---|
| CLR type | `int`, `long`, `short`, `byte`, `bool`, `decimal`, `double`, `float`, `string`, `Guid`, `DateTime`, `byte[]` |
| Nullability (`IsNullable`) | Lifts the inner strategy via `OneOf(inner, Constant(null))` |
| `MaxLength` (string, byte[]) | Bounded length on the underlying primitive strategy |
| `Precision`/`Scale` (decimal) | Bounds magnitude and fractional digits |
| `ValueGenerated` (`OnAdd`, `OnAddOrUpdate`) | Returns CLR default — EF assigns the real value on insert |

Out of scope in v1: `CheckConstraint`, `HasConversion` value converters, unique-index dedup. See [explanation](../explanation/efcore-property-testing.md) for why.

---

## `RoundtripAsserter`

```csharp
namespace Conjecture.EFCore;

public static class RoundtripAsserter
{
    public static Task AssertRoundtripAsync<TEntity>(
        Func<DbContext> factory,
        TEntity entity,
        IEqualityComparer<TEntity>? comparer = null,
        CancellationToken cancellationToken = default) where TEntity : class;

    public static Task AssertNoTrackingMatchesTrackedAsync<TEntity>(
        Func<DbContext> factory,
        TEntity entity,
        IEqualityComparer<TEntity>? comparer = null,
        CancellationToken cancellationToken = default) where TEntity : class;
}
```

| Method | Behaviour |
|---|---|
| `AssertRoundtripAsync` | Saves `entity` via `factory()`, opens a *fresh* `DbContext`, reloads by primary key, and compares using `comparer` (or a default scalar-property comparer that skips navigations). Throws `RoundtripAssertionException` on mismatch. |
| `AssertNoTrackingMatchesTrackedAsync` | Saves, then reads the entity twice from fresh contexts: once tracked (`Find`), once `AsNoTracking`. Compares the two. Catches navigation/include divergence. |

The default comparer walks `entityType.GetProperties()` and reads each value via `IProperty.PropertyInfo!.GetValue(entity)` — so cross-context attach is never triggered. The exception message includes the offending property name and both values.

```csharp
await RoundtripAsserter.AssertRoundtripAsync(
    () => new AppDbContext(_options),
    new Order { Id = Guid.NewGuid(), PlacedAt = DateTimeOffset.UtcNow });
```

### `RoundtripAssertionException`

```csharp
public sealed class RoundtripAssertionException : Exception
{
    public RoundtripAssertionException(string message);
}
```

---

## `MigrationHarness`

```csharp
namespace Conjecture.EFCore;

public static class MigrationHarness
{
    public static Task AssertUpDownIdempotentAsync(
        DbContext context,
        CancellationToken cancellationToken = default);
}
```

Applies all pending migrations forward, snapshots the resulting schema (via `sqlite_master` for SQLite), runs the latest migration's `Down`, then re-applies its `Up` and asserts the post-roundtrip schema matches the snapshot. Catches asymmetric migrations that apply forward cleanly but leave residue on rollback.

Throws `MigrationAssertionException` if the snapshots diverge. Throws `InvalidOperationException` if no migrations are defined. Throws `NotSupportedException` for non-SQLite providers in v1.

The rollback step uses `IMigrator.MigrateAsync(rollbackTarget)` — the same path EF runs in production. No raw SQL bypass.

```csharp
await using AppDbContext db = new AppDbContext(sqliteOptions);
await MigrationHarness.AssertUpDownIdempotentAsync(db);
```

### `MigrationAssertionException`

```csharp
public sealed class MigrationAssertionException : Exception
{
    public MigrationAssertionException(string message);
    public MigrationAssertionException(string message, Exception innerException);
}
```

---

## See also

- [Tutorial: Property tests for EF Core](../tutorials/10-efcore-integration.md)
- [How-to: Set up EF Core property testing](../how-to/setup-efcore-property-testing.md)
- [How-to: Test migration up/down invariants](../how-to/test-efcore-migrations.md)
- [How-to: Customise EF Core entity generation](../how-to/customise-efcore-entity-generation.md)
- [Explanation: Why property testing finds EF Core bugs](../explanation/efcore-property-testing.md)
- [ADR 0065: Conjecture.EFCore package design](../../decisions/0065-conjecture-efcore-package-design.md)

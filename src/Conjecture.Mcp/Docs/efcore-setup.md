# Conjecture.EFCore Setup Guide

Step-by-step guide for wiring up `Conjecture.EFCore` in a test project.

## 1. Add the NuGet package

```xml
<PackageReference Include="Conjecture.EFCore" />
```

## 2. Generate entities with `Generate.Entity<T>`

Use `Generate.Entity<T>(context)` to produce property-based strategies for EF Core entity types tracked by a `DbContext`:

```csharp
using Conjecture.EFCore;

[Property]
public async Task OrderRoundtrips(AppDbContext db, Strategy<Order> orderStrategy)
{
    Order order = orderStrategy.Example();
    await RoundtripAsserter.AssertRoundtripsAsync(db, order);
}
```

The strategy draws values that satisfy all EF Core model constraints registered on the entity.

## 3. Sample existing rows with `Generate.EntitySet<T>`

Use `Generate.EntitySet<T>(context)` to draw from rows already persisted in the database:

```csharp
using Conjecture.EFCore;

Generate.EntitySet<Order>(db)
// → Strategy<Order> sampling from existing rows in DbSet<Order>
```

## 4. Assert roundtrips with `RoundtripAsserter`

`RoundtripAsserter.AssertRoundtripsAsync` saves an entity, reloads it, and asserts value equality:

```csharp
await RoundtripAsserter.AssertRoundtripsAsync(db, order);
```

If the reload does not match the original, a `RoundtripAssertionException` is thrown with a diff.

## 5. Test schema migrations with `MigrationHarness`

`MigrationHarness` applies all pending EF Core migrations and verifies the schema round-trips without data loss:

```csharp
using Conjecture.EFCore;

await MigrationHarness.AssertMigrationsApplyCleanlyAsync<AppDbContext>(connectionString);
```

If a migration fails to apply or rolls back incorrectly, a `MigrationAssertionException` is thrown.

## 6. Apply the `[Property]` attribute

Use the standard `[Property]` attribute from your test framework adapter:

```csharp
[Property]
public async Task OrdersRoundtripViaEFCore(AppDbContext db, Strategy<Order> strategy)
{
    Order order = strategy.Example();
    await RoundtripAsserter.AssertRoundtripsAsync(db, order);
}
```

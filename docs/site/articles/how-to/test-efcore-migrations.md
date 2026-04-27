# Test migration up/down invariants

`MigrationHarness.AssertUpDownIdempotentAsync` applies all pending migrations to head, snapshots the schema, runs the latest migration's `Down`, then re-applies its `Up` and asserts the resulting schema matches the snapshot. Use it to catch migrations that apply forward but break backward — a common source of staging/production drift.

## When to use it

- You have a `DbContext` with a real EF migrations history (not `EnsureCreated`).
- You want every PR that touches a migration to fail CI if the `Down` is asymmetric.
- You target SQLite (the supported provider in v1).

> [!NOTE]
> v1 is SQLite-only. The harness throws `NotSupportedException` for other providers. ADR 0065 covers why and what would be needed for multi-provider support.

## Prerequisites

- A `DbContext` configured for SQLite.
- At least one migration committed under `Migrations/`.
- The default test provider from [the setup guide](setup-efcore-property-testing.md) — shared in-memory connection so `Migrate()` can apply against an isolated database per test.

## Basic usage

```csharp
using Conjecture.EFCore;

[Fact]
public async Task Latest_Migration_RoundTrips()
{
    await using SqliteAppDbContextFactory factory = new();
    await using AppDbContext db = factory.Create();
    await MigrationHarness.AssertUpDownIdempotentAsync(db);
}
```

The harness:

1. Calls `db.Database.MigrateAsync()` to bring the schema to head.
2. Snapshots `sqlite_master` (excluding the `__EFMigrationsHistory` table and `sqlite_%` system rows).
3. Calls `migrator.MigrateAsync(rollbackTarget)` where `rollbackTarget` is the prior migration ID (or `Migration.InitialDatabase = "0"` if there's only one).
4. Calls `migrator.MigrateAsync()` again to return to head.
5. Re-snapshots, compares, and throws `MigrationAssertionException` if they differ.

## Common failure modes

The harness catches:

- **`Down` drops a table that `Up` only altered.** The next `Up` rebuilds it from the model snapshot — and any data you intended to preserve is gone.
- **`Down` forgets to drop an index `Up` created.** Re-running `Up` against the surviving index throws on conflicting names.
- **`Down` recreates a column under a slightly different definition.** The post-roundtrip `sqlite_master.sql` text differs and the harness reports the divergence.

## Sample failure message

```text
Conjecture.EFCore.MigrationAssertionException:
Schema diverged after Down/Up roundtrip.

Before:
  table  Orders  CREATE TABLE "Orders" ("Id" TEXT NOT NULL CONSTRAINT "PK_Orders" PRIMARY KEY, "Total" TEXT NOT NULL)
After:
  table  Orders  CREATE TABLE "Orders" ("Id" TEXT NOT NULL CONSTRAINT "PK_Orders" PRIMARY KEY, "Total" TEXT NOT NULL)
  index  IX_Orders_Total  CREATE INDEX "IX_Orders_Total" ON "Orders" ("Total")
```

The extra `IX_Orders_Total` row reveals that the latest migration's `Down` didn't drop the index it created.

## SQLite-friendly migrations

SQLite cannot natively `DROP COLUMN` or `ALTER COLUMN` on older table-rebuild paths. EF Core 10 still supports these via table rebuild, but the rebuild itself can perturb implicit ordering and trip the snapshot comparison.

For predictable rollbacks, prefer:

- `CreateTable` / `DropTable`
- `CreateIndex` / `DropIndex`
- `AddColumn` (additive — the symmetric `Down` is `DropColumn`, but the round-trip is brittle on SQLite; consider a forward-only schema policy or test on a richer provider via the integration self-test tier)

If your migrations rely on `DropColumn` round-trips and your CI runs SQLite, gate the harness assertion behind a per-test attribute or run it only against migrations that exercise additive shapes.

## Integration with property tests

Combine the harness with a property test that generates entity graphs against the *current* schema and rounds them through `Down` + `Up`:

```csharp
[Property]
public async Task Migration_Roundtrip_Preserves_Existing_Rows(Strategy<Order> orders)
{
    await using SqliteAppDbContextFactory factory = new();
    await using AppDbContext db = factory.Create();

    db.Orders.Add(orders.Sample());
    await db.SaveChangesAsync();

    await MigrationHarness.AssertUpDownIdempotentAsync(db);

    // After roundtrip, the row should still be reachable.
    Assert.Equal(1, await db.Orders.CountAsync());
}
```

The first failure shrinks to the smallest `Order` that triggers the divergence — usually a column-specific edge case (a `null` where a `Down` recreates `NOT NULL`, etc.).

## See also

- [Reference: MigrationHarness](../reference/efcore.md#migrationharness)
- [ADR 0065: Conjecture.EFCore package design](../../decisions/0065-conjecture-efcore-package-design.md)

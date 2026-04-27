# Assert cascade correctness

`AspNetCoreEFCoreInvariants.AssertCascadeCorrectnessAsync` walks the EF Core model for foreign keys whose principal is the deleted root, then verifies dependent rows behave per the configured `DeleteBehavior` after the request returns. Use it to catch broken cascade configurations, mistyped `OnDelete` rules, and migrations that drift from the model.

## When to use it

- Your endpoint deletes an aggregate root that has dependents (orders → line items, customers → addresses, etc.).
- You want the cascade to match the EF model regardless of provider quirks.
- You want the test to fail loudly if a future migration silently changes a relationship's `DeleteBehavior`.

> [!WARNING]
> SQLite is the recommended backing provider for this invariant. EF's InMemory provider emulates cascades in process and can drift from real SQL — a passing assertion against InMemory does not guarantee the same behaviour on PostgreSQL or SQL Server. See [ADR 0066](../../decisions/0066-conjecture-aspnetcore-efcore-package-design.md).

## Prerequisites

- The composite test fixture from [Tutorial 11](../tutorials/11-aspnetcore-efcore-integration.md), backed by `SqliteDbTarget` (or a SQLite-configured `AspNetCoreDbTarget<TContext>`).
- A delete endpoint (`DELETE /<root>/{id}`) on the test app.
- At least one dependent entity with a foreign key into the root.

## Recipe

```csharp
using Conjecture.AspNetCore.EFCore;
using Conjecture.Core;

[Property]
public async Task DeleteCustomer_CascadesPerModel(Strategy<Customer> customers)
{
    Customer existing = await SeedCustomerWithOrdersAsync(customers.Sample());

    await invariants.AssertCascadeCorrectnessAsync(
        (client, ct) => client.DeleteAsync($"/customers/{existing.Id}", ct),
        typeof(Customer));
}
```

The asserter:

1. Captures the before-snapshot.
2. Runs `deleteRequest`. If status ≥ 400, returns early without asserting (compose with `AssertNoPartialWritesOnErrorAsync` for that path).
3. Walks `IModel.GetEntityTypes()` for every `IForeignKey` whose `PrincipalEntityType.ClrType == rootEntityType`.
4. For each FK, queries dependents via the live `DbContext` and asserts the row state matches the configured `DeleteBehavior`:

| `DeleteBehavior` | Expected post-delete state |
|---|---|
| `Cascade`, `ClientCascade` | Dependents removed |
| `SetNull`, `ClientSetNull` | Dependents survive; FK column is `NULL` |
| `Restrict`, `NoAction` | Dependents unchanged (the request should have returned 4xx — composes with no-partial-writes) |

5. Throws `AspNetCoreEFCoreInvariantException` with the FK name and observed delta on any mismatch.

## Pattern: Cascade root with multiple dependent types

`AssertCascadeCorrectnessAsync` enumerates *every* required foreign key into `rootEntityType`. One call covers the entire dependent graph:

```csharp
modelBuilder.Entity<Customer>()
    .HasMany(c => c.Orders)
    .WithOne(o => o.Customer)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<Customer>()
    .HasMany(c => c.Addresses)
    .WithOne(a => a.Customer)
    .OnDelete(DeleteBehavior.SetNull);

// One assertion exercises both relationships:
await invariants.AssertCascadeCorrectnessAsync(
    (client, ct) => client.DeleteAsync($"/customers/{id}", ct),
    typeof(Customer));
```

Orders are removed; addresses survive with null `CustomerId`. The invariant verifies both halves.

## Sample failure

```text
Conjecture.AspNetCore.EFCore.AspNetCoreEFCoreInvariantException:
Cascade violation on FK Order.CustomerId → Customer.Id (DeleteBehavior=Cascade):
expected dependents removed, observed 2 row(s) still present.
Order: 0 added, 0 removed (expected -2 from cascade)
```

The shrunk counterexample is usually a `Customer` with one or two `Orders` — the smallest graph that exercises the broken cascade.

## See also

- [How-to: Assert no partial writes on 4xx/5xx](test-aspnetcore-efcore-no-partial-writes.md)
- [How-to: Assert endpoint idempotency](test-aspnetcore-efcore-idempotency.md)
- [Reference: AssertCascadeCorrectnessAsync](../reference/aspnetcore-efcore.md#assertcascadecorrectnessasync)
- [ADR 0066: Conjecture.AspNetCore.EFCore package design](../../decisions/0066-conjecture-aspnetcore-efcore-package-design.md)

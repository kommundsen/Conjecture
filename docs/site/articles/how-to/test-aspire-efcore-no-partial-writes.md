# Assert no partial writes on error (Aspire + EF Core)

`AspireEFCoreInvariants.AssertNoPartialWritesOnErrorAsync` snapshots the row count before and after a generated interaction and fails if the interaction threw but the count changed. Use it to catch handlers that raise an exception but have already mutated the database — a common shape for missed transactions and swallowed exceptions in out-of-process services.

## When to use it

- A service receives HTTP or messaging interactions and writes to a database.
- Domain validation or downstream calls happen *after* the first entity mutation.
- You want every error path to imply zero net persisted change.

## Prerequisites

A configured `AspireDbTarget<TContext>` and an `IInteractionTarget` (HTTP target or messaging target) — see [Tutorial 12](../tutorials/12-aspire-efcore-integration.md) for the full fixture setup.

## Recipe

```csharp
using Conjecture.Aspire.EFCore;
using Conjecture.Core;
using Conjecture.Http;
using Conjecture.Xunit.V3;

public class OrderInvariantTests(StoreFixture fixture)
    : IClassFixture<StoreFixture>
{
    private readonly AspireEFCoreInvariants invariants =
        new(
            writer: fixture.HttpTarget,
            db: fixture.Orders);

    [Property]
    public async Task PostOrders_NoPartialWritesOnError(Strategy<Order> orders)
    {
        Order payload = orders.Sample();

        HttpInteraction operation = new(
            ResourceName: "orders-api",
            Method: "POST",
            Path: "/orders",
            Body: payload,
            Headers: null);

        await invariants.AssertNoPartialWritesOnErrorAsync(
            operation: operation,
            rowCount: ctx => ctx.Set<Order>().CountAsync());
    }
}
```

The asserter:

1. Captures the row count via `rowCount` before the interaction.
2. Executes the interaction; catches any exception thrown.
3. Captures the row count again after the interaction.
4. If the interaction threw *and* the count changed, throws `AspireEFCoreInvariantException`.

Interactions that succeed do not trigger the assertion — this invariant is intentionally narrow to error paths.

## Messaging interactions

Pass a messaging `IInteractionTarget` instead of HTTP:

```csharp
using Conjecture.Aspire.EFCore;
using Conjecture.Core;
using Conjecture.Messaging;
using Conjecture.Xunit.V3;

public class OrderInvariantTests(StoreFixture fixture)
    : IClassFixture<StoreFixture>
{
    private readonly AspireEFCoreInvariants messagingInvariants =
        new(writer: fixture.MessagingTarget, db: fixture.Orders);

    [Property]
    public async Task PlaceOrderMessage_NoPartialWritesOnError(Strategy<Order> orders)
    {
        Order payload = orders.Sample();
        MessageInteraction operation = fixture.MessagingTarget.BuildMessage("orders.place", payload);

        await messagingInvariants.AssertNoPartialWritesOnErrorAsync(
            operation: operation,
            rowCount: ctx => ctx.Set<Order>().CountAsync());
    }
}
```

## Common patterns

### Filtering for write interactions only

Read operations (`GET /…`) cannot leave partial writes, so filtering keeps the property focused:

```csharp
Strategy<HttpInteraction> writeOperations = Generate.OneOf(
    postOrders,
    putOrders,
    deleteOrders);
```

## Sample failure

```text
Conjecture.Aspire.EFCore.AspireEFCoreInvariantException:
AssertNoPartialWritesOnError: interaction threw but row count changed from 0 to 1 — partial write detected.

Counterexample:
  Order { Id = e8d8b9c2-…, Customer = "", Total = -0.01 }
```

## See also

- [How-to: Assert idempotency (Aspire + EF Core)](test-aspire-efcore-idempotency.md)
- [How-to: Test eventual consistency](test-aspire-efcore-eventual-consistency.md)
- [Reference: AspireEFCoreInvariants](../reference/aspire-efcore.md#aspireefcoreinvariants)
- [Explanation: Why Aspire+EFCore composite testing works](../explanation/aspire-efcore-composite-testing.md)

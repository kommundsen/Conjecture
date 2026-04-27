# Assert idempotency (Aspire + EF Core)

`AspireEFCoreInvariants.AssertIdempotentAsync` runs the same generated interaction twice and asserts the second call leaves the database row count identical to the first. Use it to verify that out-of-process services honour the idempotency contracts their messaging or HTTP clients rely on.

## When to use it

- A consumer processes at-least-once delivered messages (Azure Service Bus, RabbitMQ).
- An HTTP client retries `PUT` or `DELETE` requests on transient failures.
- You want the test to fail loudly if a refactor introduces a duplicate insert or audit counter increment.

## Prerequisites

A configured fixture with `AspireDbTarget<TContext>` and an `IInteractionTarget` (HTTP target or messaging target) — see [Tutorial 12](../tutorials/12-aspire-efcore-integration.md).

## Recipe

```csharp
using Conjecture.Aspire.EFCore;
using Conjecture.Core;
using Conjecture.Http;
using Conjecture.Xunit.V3;

public class OrderIdempotencyTests(StoreFixture fixture)
    : IClassFixture<StoreFixture>
{
    private readonly AspireEFCoreInvariants invariants =
        new(
            writer: fixture.HttpTarget,
            db: fixture.Orders);

    [Property]
    public async Task PutOrder_IsIdempotent(Strategy<Order> orders)
    {
        Order payload = orders.Sample();

        HttpInteraction operation = new(
            ResourceName: "orders-api",
            Method: "PUT",
            Path: $"/orders/{payload.Id}",
            Body: payload,
            Headers: null);

        await invariants.AssertIdempotentAsync(
            operation: operation,
            rowCount: ctx => ctx.Set<Order>().CountAsync(),
            eventualTimeout: TimeSpan.FromSeconds(5));
    }
}
```

The `eventualTimeout` controls how long `AssertIdempotentAsync` polls for the DB row count to settle after the second call. This is the Aspire variant's key difference from the in-process `AspNetCoreEFCoreInvariants`: writes happen out-of-process, so the asserter must wait for eventual consistency before comparing snapshots.

The asserter:

1. Runs the interaction once; captures `afterFirst` via `rowCount`.
2. Runs the interaction again.
3. Polls until `rowCount` equals `afterFirst` within `eventualTimeout`, or times out.
4. Throws `AspireEFCoreInvariantException` with before/after counts if convergence does not occur.

## Messaging interactions

Pass a messaging `IInteractionTarget` instead of HTTP:

```csharp
using Conjecture.Aspire.EFCore;
using Conjecture.Core;
using Conjecture.Messaging;
using Conjecture.Xunit.V3;

public class OrderIdempotencyTests(StoreFixture fixture)
    : IClassFixture<StoreFixture>
{
    private readonly AspireEFCoreInvariants invariants =
        new(
            writer: fixture.MessagingTarget,
            db: fixture.Orders);

    [Property]
    public async Task PlaceOrderMessage_IsIdempotent(Strategy<Order> orders)
    {
        Order order = orders.Sample();
        MessageInteraction operation = fixture.MessagingTarget.BuildMessage("orders.place", order);

        await invariants.AssertIdempotentAsync(
            operation: operation,
            rowCount: ctx => ctx.Set<Order>().CountAsync(),
            eventualTimeout: TimeSpan.FromSeconds(10));
    }
}
```

## Picking `eventualTimeout`

| Scenario | Recommended `eventualTimeout` |
|----------|-------------------------------|
| HTTP → sync DB write (in-process) | `TimeSpan.FromSeconds(1)` |
| Message → async consumer (local emulator) | `TimeSpan.FromSeconds(10)` |
| Message → async consumer (RabbitMQ container) | `TimeSpan.FromSeconds(5)` |

Start with 5 seconds and tune down once shrinking completes in a reasonable time.

## Sample failure

**Idempotency violation (consumer that inserts a fresh row on each delivery):**

```text
Conjecture.Aspire.EFCore.AspireEFCoreInvariantException:
AssertIdempotent: row count after second call (2) did not converge to count after first call (1) within 5.0s.
```

The shrunk counterexample is typically the smallest message or payload where the consumer fails to detect the duplicate — an off-by-one key check, a missing `ON CONFLICT DO NOTHING`, or a unique-constraint violation the handler did not convert into a no-op.

## See also

- [How-to: Assert no partial writes (Aspire + EF Core)](test-aspire-efcore-no-partial-writes.md)
- [How-to: Test eventual consistency](test-aspire-efcore-eventual-consistency.md)
- [Reference: AspireEFCoreInvariants](../reference/aspire-efcore.md#aspireefcoreinvariants)

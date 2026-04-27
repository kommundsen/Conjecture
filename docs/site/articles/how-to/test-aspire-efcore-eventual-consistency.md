# Test eventual consistency across Aspire services

`IDbTargetWaitForExtensions.WaitForAsync` polls an `AspireDbTarget<TContext>` until a predicate returns `true` or a timeout expires. Use it to assert that a cross-service write eventually becomes visible — the correct model for out-of-process Aspire scenarios where there is no synchronous "flush everything" primitive.

## When to use it

- A service processes a message or HTTP command and writes to a database asynchronously.
- Your test sends the trigger and needs to assert the DB state after eventual completion.
- You cannot call `SaveChangesAsync` directly from the test (the write happens inside the service under test).

## Prerequisites

A configured `AspireDbTarget<TContext>` — see [Set up Aspire.EFCore property testing](setup-aspire-efcore-property-testing.md).

## Recipe

```csharp
using Conjecture.Aspire.EFCore;

// After triggering the write (HTTP, message, gRPC, etc.)
await fixture.Orders.WaitForAsync(
    predicate: ctx => ctx.Orders.AnyAsync(o => o.Id == orderId),
    timeout: TimeSpan.FromSeconds(5));
```

`WaitForAsync` resolves a fresh `DbContext` on each poll. It retries until `predicate(ctx)` returns `true` or `timeout` elapses. On timeout it throws `TimeoutException` with the resource name and elapsed time.

## Default backoff

The extension uses a 50 ms → 250 ms exponential back-off by default:

| Poll | Delay |
|------|-------|
| 1 | 50 ms |
| 2 | 100 ms |
| 3 | 200 ms |
| 4+ | 250 ms (capped) |

## Custom `pollInterval`

Override with a fixed interval for deterministic test timing:

```csharp
await fixture.Orders.WaitForAsync(
    predicate: ctx => ctx.Orders.CountAsync().Result >= expectedCount,
    timeout: TimeSpan.FromSeconds(10),
    pollInterval: TimeSpan.FromMilliseconds(100));
```

> [!TIP]
> Short poll intervals keep property tests fast during shrinking. Long intervals accumulate across many examples. The default back-off is calibrated for typical Aspire container response times (Postgres < 50 ms, SQL Server < 150 ms).

## Picking a `timeout`

| Scenario | Recommended `timeout` |
|----------|-----------------------|
| In-process write via DI scope | `TimeSpan.FromSeconds(1)` |
| Async message handler (Azure Service Bus emulator) | `TimeSpan.FromSeconds(10)` |
| RabbitMQ with consumer prefetch=1 | `TimeSpan.FromSeconds(5)` |
| gRPC server-side streaming response | `TimeSpan.FromSeconds(3)` |

Start with 5 seconds and tune down once you see median poll counts in the test output.

## Multiple resources

`WaitForAsync` is an extension on `IDbTarget` — call it on any registered target:

```csharp
await fixture.Orders.WaitForAsync(
    predicate: ctx => ctx.Orders.AnyAsync(o => o.Id == orderId),
    timeout: TimeSpan.FromSeconds(5));

await fixture.Catalog.WaitForAsync(
    predicate: ctx => ctx.Products.AnyAsync(p => p.IsAvailable),
    timeout: TimeSpan.FromSeconds(5));
```

## Common failures

### `TimeoutException: WaitForAsync on 'orders-db' timed out after 5.0 s`

The write never arrived. Common causes:

- The message was not enqueued — check the publisher call preceding `WaitForAsync`.
- The consumer crashed silently — check the Aspire dashboard for the worker service health.
- The predicate is wrong — the row arrived but `predicate` always returns `false`.

### Flaky pass/fail on the same seed

The timeout is too close to the actual write latency. Raise `timeout` by 2×; if the test still flakes, profile the consumer under load.

## Sample failure output

```text
System.TimeoutException:
WaitForAsync on resource 'orders-db' timed out after 5.0 s.
Predicate: ctx => ctx.Orders.AnyAsync(o => o.Id == e8d8b9c2-…)
```

Conjecture shrinks the generated inputs independently of `WaitForAsync` — the timeout applies per-example, not per-shrink step.

## See also

- [Reference: IDbTargetWaitForExtensions](../reference/aspire-efcore.md#idbTargetwaitforextensions)
- [How-to: Set up Aspire.EFCore property testing](setup-aspire-efcore-property-testing.md)
- [How-to: Assert no partial writes](test-aspire-efcore-no-partial-writes.md)

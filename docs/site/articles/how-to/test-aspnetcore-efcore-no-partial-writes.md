# Assert no partial writes on 4xx/5xx

`AspNetCoreEFCoreInvariants.AssertNoPartialWritesOnErrorAsync` snapshots the database before and after a generated request and fails if an error response (status code ≥ 400) leaves persisted state. Use it to catch handlers that return 4xx/5xx but have already mutated the change tracker — a common shape of "missed `await`", swallowed exceptions, and incomplete transactions.

## When to use it

- Your endpoint writes to a database before deciding the response.
- Domain validation or downstream calls happen *after* the first `Add` / `Update` / `Remove`.
- You want every failure response to imply zero persisted change.

## Prerequisites

A configured composite test fixture — see [Tutorial 11](../tutorials/11-aspnetcore-efcore-integration.md) for the `WebApplicationFactory<TestApp>` + `HostHttpTarget` + `AspNetCoreDbTarget<TContext>` setup.

## Recipe

```csharp
using Conjecture.AspNetCore.EFCore;
using Conjecture.Core;

[Property]
public async Task PostOrders_NeverPartialWritesOnError(Strategy<Order> orders)
{
    Order payload = orders.Sample();

    await invariants.AssertNoPartialWritesOnErrorAsync(
        (client, ct) => client.PostAsJsonAsync("/orders", payload, ct));
}
```

The lambda receives an `HttpClient` (resolved from the underlying `IHttpTarget`) and a cancellation token. It must return the `HttpResponseMessage` so the asserter can inspect the status code.

The asserter:

1. Captures `EntitySnapshotter.CaptureAsync(db)` — a per-entity-type count and primary-key set across the entire model.
2. Runs the request.
3. Captures the after-snapshot.
4. If `(int)response.StatusCode >= 400` and `EntitySnapshotter.Diff(before, after).IsEmpty == false`, throws `AspNetCoreEFCoreInvariantException`.

Successful responses (2xx/3xx) skip the assertion — this invariant is intentionally narrow.

## Common patterns

### Multiple endpoints in one property

Combine endpoint discovery from `Conjecture.AspNetCore` with the invariant:

```csharp
[Property]
public async Task EveryEndpoint_NoPartialWritesOnError(
    Strategy<DiscoveredEndpoint> endpoints,
    Strategy<HttpInteraction> requests)
{
    DiscoveredEndpoint endpoint = endpoints.Sample();
    HttpInteraction interaction = requests.Sample();

    await invariants.AssertNoPartialWritesOnErrorAsync(
        (client, ct) => interaction.Send(client, endpoint, ct));
}
```

### Filtering for write endpoints only

Read endpoints (`GET /…`) cannot leave partial writes, so filtering keeps the property focused:

```csharp
Strategy<DiscoveredEndpoint> writeEndpoints = endpoints
    .Filter(e => e.HttpMethod is "POST" or "PUT" or "PATCH" or "DELETE");
```

## Sample failure

```text
Conjecture.AspNetCore.EFCore.AspNetCoreEFCoreInvariantException:
Endpoint POST /orders returned 500 but persisted 1 row(s).
Order: +1 (added [e8d8b9c2-…])
```

The diff report uses the same `EntitySnapshotDiff.ToReport()` format as the rest of `Conjecture.EFCore`, so the message format is consistent across `RoundtripAsserter`, `MigrationHarness`, and the composite invariants.

## See also

- [How-to: Assert cascade correctness](test-aspnetcore-efcore-cascades.md)
- [How-to: Assert endpoint idempotency](test-aspnetcore-efcore-idempotency.md)
- [Reference: AspNetCoreEFCoreInvariants](../reference/aspnetcore-efcore.md#aspnetcoreefcoreinvariants)
- [Explanation: Why composite invariants find bugs](../explanation/aspnetcore-efcore-composite-testing.md)

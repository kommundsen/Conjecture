# Assert endpoint idempotency

`AspNetCoreEFCoreInvariants.AssertIdempotentAsync` runs the same generated request twice and asserts the second call leaves the database observably identical to the first — the canonical definition of idempotency for write endpoints. Endpoints opt in via the builder predicate `MarkIdempotent`.

## When to use it

- Your API contract advertises certain endpoints as idempotent (typically `PUT`, `DELETE`, or upsert-style `POST`).
- Clients rely on safe replay (network retry, at-least-once delivery, browser-back navigation).
- You want the test to fail loudly if a refactor introduces a counter increment or duplicate insert.

## Why opt-in?

HTTP-verb inference is too lossy: `POST` is often idempotent (upserts), `PUT` can have side effects (counter increments), and `DELETE` is usually but not always idempotent. The predicate lets you match exactly the endpoints your API contract claims, with zero false positives. See [ADR 0066](../../decisions/0066-conjecture-aspnetcore-efcore-package-design.md) for the design discussion.

## Prerequisites

- The composite test fixture from [Tutorial 11](../tutorials/11-aspnetcore-efcore-integration.md).
- An endpoint your API contract treats as idempotent.

## Recipe

```csharp
using Conjecture.AspNetCore;
using Conjecture.AspNetCore.EFCore;
using Conjecture.Core;

private readonly AspNetCoreEFCoreInvariants invariants =
    new AspNetCoreEFCoreInvariants(http, db)
        .MarkIdempotent(endpoint =>
            endpoint.HttpMethod is "PUT" or "DELETE"
            || endpoint.RoutePattern.StartsWith("/api/upserts/"));

[Property]
public async Task PutOrder_IsIdempotent(Strategy<Order> orders)
{
    Order payload = orders.Sample();
    DiscoveredEndpoint endpoint = endpoints.Single(e =>
        e.HttpMethod == "PUT" && e.RoutePattern == "/orders/{id}");

    await invariants.AssertIdempotentAsync(
        (client, ct) => client.PutAsJsonAsync($"/orders/{payload.Id}", payload, ct),
        endpoint);
}
```

The asserter:

1. If `predicate(endpoint) == false`, returns silently — defensive guard for callers who compose multiple endpoints in one property.
2. Captures the before-snapshot.
3. Runs `request` once — captures `afterFirst` snapshot and `response1.StatusCode`.
4. Runs `request` again — captures `afterSecond` snapshot and `response2.StatusCode`.
5. Asserts `EntitySnapshotter.Diff(afterFirst, afterSecond).IsEmpty == true` AND `response1.StatusCode == response2.StatusCode`.
6. Throws `AspNetCoreEFCoreInvariantException` with the diff report and the endpoint route on failure.

## Common predicate shapes

```csharp
// Verb-only (most common)
.MarkIdempotent(e => e.HttpMethod is "PUT" or "DELETE")

// Route-pattern allowlist
.MarkIdempotent(e => e.RoutePattern.StartsWith("/api/upserts/")
                  || e.RoutePattern.StartsWith("/api/replace/"))

// Attribute-driven (if the production app uses a marker attribute)
.MarkIdempotent(e => e.Metadata.OfType<IdempotentAttribute>().Any())

// Mixed
.MarkIdempotent(e => e.HttpMethod == "PUT"
                  || (e.HttpMethod == "POST" && e.RoutePattern.Contains("/upsert/")))
```

## Sample failures

**Idempotency violation (POST that creates a fresh row each call):**

```text
Conjecture.AspNetCore.EFCore.AspNetCoreEFCoreInvariantException:
Endpoint POST /orders is marked idempotent but second call diverged.
Order: +1 (added [d3a1…])
```

**Status-code divergence (first 200, second 500):**

```text
Conjecture.AspNetCore.EFCore.AspNetCoreEFCoreInvariantException:
Endpoint PUT /orders/{id} is marked idempotent but status codes diverged
(first call 200, second call 500).
```

The shrunk counterexample is typically a payload where the second call hits a state-dependent guard the first call didn't trigger — an off-by-one increment, a unique-constraint collision, or a missing "if-already-saved-then-noop" branch.

## See also

- [How-to: Assert no partial writes on 4xx/5xx](test-aspnetcore-efcore-no-partial-writes.md)
- [How-to: Assert cascade correctness](test-aspnetcore-efcore-cascades.md)
- [Reference: MarkIdempotent + AssertIdempotentAsync](../reference/aspnetcore-efcore.md#markidempotent--assertidempotentasync)

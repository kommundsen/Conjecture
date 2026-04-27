# Conjecture.AspNetCore.EFCore reference

API reference for the `Conjecture.AspNetCore.EFCore` package — composite HTTP + DB property testing over `WebApplicationFactory<TEntryPoint>`. Install via:

```xml
<PackageReference Include="Conjecture.AspNetCore.EFCore" />
```

The package depends on [`Conjecture.AspNetCore`](aspnetcore-request-builder.md), [`Conjecture.EFCore`](efcore.md), and `Microsoft.AspNetCore.Mvc.Testing`. Framework adapter wiring stays in the existing satellite packages — there is no `Conjecture.AspNetCore.EFCore.Xunit` / `.NUnit` / `.MSTest` package.

> [!NOTE]
> Design rationale lives in [ADR 0066: Conjecture.AspNetCore.EFCore package design](../../decisions/0066-conjecture-aspnetcore-efcore-package-design.md).

---

## `AspNetCoreDbTarget<TContext>`

```csharp
namespace Conjecture.AspNetCore.EFCore;

public sealed class AspNetCoreDbTarget<TContext> : IDbTarget, IAsyncDisposable
    where TContext : DbContext
{
    public AspNetCoreDbTarget(IHost host, string resourceName);
    public string ResourceName { get; }
    public DbContext ResolveContext(string resourceName);
    public TContext Resolve();
    public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct);
    public Task ResetAsync(string resourceName, CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
}
```

Bridges the test host's service container to the `IDbTarget` Layer-1 contract. Every `ResolveContext` / `Resolve` call creates a fresh `IServiceScope` so the returned `DbContext` does not leak `ChangeTracker` state across examples.

| Member | Purpose |
|---|---|
| `AspNetCoreDbTarget(IHost host, string resourceName)` | Construct from `factory.Services.GetRequiredService<IHost>()` (typically obtained via xUnit `IClassFixture<WebApplicationFactory<TApp>>`). The constructor caches `host.Services.GetRequiredService<IServiceScopeFactory>()`. |
| `ResourceName` | The string passed at construction. `CompositeInteractionTarget` routes by this name. |
| `ResolveContext(string)` | Returns a freshly-scoped `DbContext` (the base type) for the requested resource. The scope is disposed when the returned context is disposed. |
| `Resolve()` | Typed convenience: same as `ResolveContext` but returns the strongly-typed `TContext` for fluent assertions. |
| `ExecuteAsync(IInteraction, CancellationToken)` | Dispatches a `DbInteraction` (`Add` / `Update` / `Remove` / `SaveChanges` / `Query`) per the `IDbTarget` contract. Throws `InvalidOperationException` for non-`DbInteraction` inputs. |
| `ResetAsync(string, CancellationToken)` | Drops and recreates the database schema on a fresh scope (`EnsureDeletedAsync` + `EnsureCreatedAsync`). |
| `DisposeAsync()` | Releases the cached `IServiceScopeFactory` reference. The owning `IHost` / `WebApplicationFactory` lifecycle is the caller's responsibility. |

```csharp
using Conjecture.AspNetCore.EFCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = factory.Services.GetRequiredService<IHost>();
AspNetCoreDbTarget<OrdersContext> orders = new(host, "orders-db");

OrdersContext ctx = orders.Resolve();
int count = await ctx.Orders.CountAsync();
```

> [!TIP]
> One target per registered `DbContext`. Apps with read-side and write-side contexts construct two `AspNetCoreDbTarget<TContext>` instances with distinct `resourceName`s and compose through `CompositeInteractionTarget`.

---

## `AspNetCoreEFCoreInvariants`

```csharp
namespace Conjecture.AspNetCore.EFCore;

public sealed class AspNetCoreEFCoreInvariants
{
    public AspNetCoreEFCoreInvariants(IHttpTarget http, IDbTarget db);

    public Task AssertNoPartialWritesOnErrorAsync(
        Func<HttpClient, CancellationToken, Task<HttpResponseMessage>> request,
        CancellationToken cancellationToken = default);

    public Task AssertCascadeCorrectnessAsync(
        Func<HttpClient, CancellationToken, Task<HttpResponseMessage>> deleteRequest,
        Type rootEntityType,
        CancellationToken cancellationToken = default);

    public AspNetCoreEFCoreInvariants MarkIdempotent(Func<DiscoveredEndpoint, bool> predicate);

    public Task AssertIdempotentAsync(
        Func<HttpClient, CancellationToken, Task<HttpResponseMessage>> request,
        DiscoveredEndpoint endpoint,
        CancellationToken cancellationToken = default);
}
```

Builder for the three composite invariants. Construct with the `IHttpTarget` (typically `HostHttpTarget`) and `IDbTarget` (`AspNetCoreDbTarget<TContext>`) sharing the same `IHost`.

### `AssertNoPartialWritesOnErrorAsync`

Captures `EntitySnapshotter.CaptureAsync(db)` before the request, runs `request`, captures after. If the response status code is ≥ 400 and the diff is non-empty, throws `AspNetCoreEFCoreInvariantException` with the status code, request method/path, and full diff report. Successful responses (2xx/3xx) skip the assertion.

```csharp
await invariants.AssertNoPartialWritesOnErrorAsync(
    (client, ct) => client.PostAsJsonAsync("/orders", payload, ct));
```

### `AssertCascadeCorrectnessAsync`

Walks `IModel.GetEntityTypes()` to enumerate every `IForeignKey` whose `PrincipalEntityType.ClrType == rootEntityType`. After `deleteRequest`, asserts dependent rows behave per the configured `DeleteBehavior`:

| `DeleteBehavior` | Expected post-delete state |
|---|---|
| `Cascade`, `ClientCascade` | Dependents removed from the DB |
| `SetNull`, `ClientSetNull` | Dependents survive with the FK column set to `NULL` |
| `Restrict`, `NoAction` | Dependents unchanged |

If the request returns ≥ 400, the method returns early — compose with `AssertNoPartialWritesOnErrorAsync` for that path.

```csharp
await invariants.AssertCascadeCorrectnessAsync(
    (client, ct) => client.DeleteAsync($"/customers/{id}", ct),
    typeof(Customer));
```

> [!WARNING]
> SQLite is the recommended backing provider for cascade invariants. EF's InMemory provider emulates cascades in memory and can drift from real SQL — a passing assertion against InMemory does not guarantee correctness against PostgreSQL or SQL Server.

### `MarkIdempotent` + `AssertIdempotentAsync`

`MarkIdempotent(predicate)` stores a `Func<DiscoveredEndpoint, bool>` and returns `this` for chaining. `AssertIdempotentAsync(request, endpoint, ct)`:

1. If no predicate matches the supplied `endpoint`, returns silently — defensive guard for callers who compose at the test level.
2. Captures `before`. Runs `request`, captures `afterFirst`. Runs `request` again, captures `afterSecond`.
3. Asserts `EntitySnapshotter.Diff(afterFirst, afterSecond).IsEmpty == true` AND `response1.StatusCode == response2.StatusCode`.
4. Throws `AspNetCoreEFCoreInvariantException` on either failure.

```csharp
AspNetCoreEFCoreInvariants invariants = new AspNetCoreEFCoreInvariants(http, db)
    .MarkIdempotent(endpoint =>
        endpoint.HttpMethod is "PUT" or "DELETE"
        || endpoint.RoutePattern.StartsWith("/api/upserts/"));

await invariants.AssertIdempotentAsync(
    (client, ct) => client.PutAsJsonAsync($"/orders/{id}", payload, ct),
    endpoint);
```

---

## `AspNetCoreEFCoreInvariantException`

```csharp
namespace Conjecture.AspNetCore.EFCore;

public sealed class AspNetCoreEFCoreInvariantException : DbInvariantException
{
    public AspNetCoreEFCoreInvariantException(string message);
    public AspNetCoreEFCoreInvariantException(string message, Exception innerException);
}
```

Derives from [`DbInvariantException`](efcore.md#dbinvariantexception). Catching `DbInvariantException` handles all DB-shape failures across the EFCore stack (Roundtrip, Migration, and these three composite invariants) uniformly.

---

## See also

- [Tutorial: Composite property tests for ASP.NET Core + EF Core](../tutorials/11-aspnetcore-efcore-integration.md)
- [How-to: Assert no partial writes on 4xx/5xx](../how-to/test-aspnetcore-efcore-no-partial-writes.md)
- [How-to: Assert cascade correctness](../how-to/test-aspnetcore-efcore-cascades.md)
- [How-to: Assert endpoint idempotency](../how-to/test-aspnetcore-efcore-idempotency.md)
- [Explanation: Why composite HTTP+DB invariants find bugs](../explanation/aspnetcore-efcore-composite-testing.md)
- [Reference: Conjecture.EFCore](efcore.md)
- [Reference: Conjecture.AspNetCore](aspnetcore-request-builder.md)
- [ADR 0066: Conjecture.AspNetCore.EFCore package design](../../decisions/0066-conjecture-aspnetcore-efcore-package-design.md)

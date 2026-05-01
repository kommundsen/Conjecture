# Conjecture.Aspire.EFCore reference

API reference for the `Conjecture.Aspire.EFCore` package — composite out-of-process property testing over `Aspire.Hosting.Testing.DistributedApplication`. Install via:

```xml
<PackageReference Include="Conjecture.Aspire.EFCore" />
```

The package depends on [`Conjecture.EFCore`](efcore.md) and [`Conjecture.Aspire`](aspire.md). It does **not** depend on `Conjecture.AspNetCore.EFCore` — the two composite packages sit at the same architectural level and are independently composable.

> [!NOTE]
> Design rationale lives in [Explanation: Why Aspire+EFCore composite testing works](../explanation/aspire-efcore-composite-testing.md).

---

## `AspireDbTarget<TContext>`

```csharp
namespace Conjecture.Aspire.EFCore;

public sealed class AspireDbTarget<TContext> : IDbTarget, IAsyncDisposable
    where TContext : DbContext
{
    public string ResourceName { get; }
    public TContext Resolve();
    public DbContext ResolveContext(string resourceName);
    public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct);
    public Task ResetAsync(string resourceName, CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();
}
```

### Factory methods

```csharp
// From a DistributedApplication (preferred in fixtures)
public static Task<AspireDbTarget<TContext>> CreateAsync(
    DistributedApplication app,
    string resourceName,
    Func<string, TContext> contextFactory,
    CancellationToken ct = default);

// From a raw connection string resolver (advanced / custom wiring)
public static Task<AspireDbTarget<TContext>> CreateAsync(
    ConnectionStringResolver resolver,
    string resourceName,
    Func<string, TContext> contextFactory,
    CancellationToken ct = default);
```

`CreateAsync(DistributedApplication, …)` calls `app.GetConnectionStringAsync(resourceName)` and passes the resolved connection string to `contextFactory`. Aspire waits for the resource to be healthy before returning, so `CreateAsync` implicitly waits for the container.

`contextFactory` receives the connection string and returns a `TContext`. The factory is called on every `Resolve()` / `ResolveContext()` call — one fresh context per invocation, no shared `ChangeTracker` state.

| Member | Purpose |
|---|---|
| `ResourceName` | The string passed at construction. `CompositeInteractionTarget` routes by this name. |
| `Resolve()` | Returns a freshly-constructed `TContext` via `contextFactory`. Caller owns the context lifetime. |
| `ResolveContext(string)` | Returns the base `DbContext` for cross-type dispatch (used internally by `CompositeInteractionTarget`). |
| `ExecuteAsync(IInteraction, CancellationToken)` | Dispatches a `DbInteraction` per the `IDbTarget` contract. Throws `InvalidOperationException` for non-`DbInteraction` inputs. |
| `ResetAsync(string, CancellationToken)` | Drops and recreates the schema (`EnsureDeletedAsync` + `EnsureCreatedAsync`). |
| `DisposeAsync()` | Releases internal resources. The `DistributedApplication` lifecycle is the caller's responsibility. |

```csharp
AspireDbTarget<OrdersContext> orders =
    await AspireDbTarget.CreateAsync<OrdersContext>(
        app,
        resourceName: "orders-db",
        contextFactory: cs => new OrdersContext(
            new DbContextOptionsBuilder<OrdersContext>()
                .UseNpgsql(cs)
                .Options));

OrdersContext ctx = orders.Resolve();
int count = await ctx.Orders.CountAsync();
```

> [!TIP]
> One target per Aspire DB resource. Multi-database apps construct one `AspireDbTarget<TContext>` per resource and compose via `CompositeInteractionTarget`.

---

## `IDbTargetWaitForExtensions`

```csharp
namespace Conjecture.Aspire.EFCore;

public static class IDbTargetWaitForExtensions
{
    public static Task WaitForAsync<TContext>(
        this IDbTarget target,
        Func<TContext, Task<bool>> predicate,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default)
        where TContext : DbContext;
}
```

Extension on `IDbTarget` (and `AspireDbTarget<TContext>` specifically) for eventual-consistency polling. Ships in `Conjecture.Aspire.EFCore` but the extension point is the `IDbTarget` contract — any `IDbTarget` implementation can be polled without additional infrastructure.

`predicate` receives a freshly-resolved `TContext` on each poll. The extension retries with 50 ms → 250 ms exponential back-off until `predicate` returns `true` or `timeout` elapses. On timeout, throws `TimeoutException` naming the resource and elapsed duration.

Pass `pollInterval` to override the default back-off with a fixed interval.

```csharp
await orders.WaitForAsync(
    predicate: ctx => ctx.Orders.AnyAsync(o => o.Id == orderId),
    timeout: TimeSpan.FromSeconds(5));
```

---

## `AspireDbTargetRegistry`

```csharp
namespace Conjecture.Aspire.EFCore;

public sealed class AspireDbTargetRegistry : IAsyncDisposable
{
    public IReadOnlyList<IDbTarget> Targets { get; }
    public AspireDbTargetRegistry Register(IDbTarget target);
    public Task ResetAllAsync(CancellationToken ct = default);
    public ValueTask DisposeAsync();
}
```

Collects `IDbTarget` instances and resets them all between property examples. Construct an instance with `new()`, then call `Register` once per target, and call `ResetAllAsync` from `IAspireAppFixture.ResetAsync`.

`ResetAllAsync` calls `target.ResetAsync(target.ResourceName, ct)` on each registered target sequentially. Each reset calls `EnsureDeletedAsync` + `EnsureCreatedAsync`, dropping and recreating the schema.

```csharp
AspireDbTargetRegistry registry = new();
registry.Register(ordersTarget)
        .Register(catalogTarget);

// In IAspireAppFixture.ResetAsync:
public Task ResetAsync(DistributedApplication app, CancellationToken ct = default)
    => registry.ResetAllAsync(ct);
```

---

## `AspireEFCoreInvariants`

```csharp
namespace Conjecture.Aspire.EFCore;

public sealed class AspireEFCoreInvariants
{
    public AspireEFCoreInvariants(IInteractionTarget writer, IDbTarget db);

    public Task AssertNoPartialWritesOnErrorAsync(
        IInteraction operation,
        Func<DbContext, Task<int>> rowCount,
        CancellationToken ct = default);

    public Task AssertIdempotentAsync(
        IInteraction operation,
        Func<DbContext, Task<int>> rowCount,
        TimeSpan eventualTimeout,
        CancellationToken ct = default);
}
```

Builder for the two composite invariants. Construct with an `IInteractionTarget` (`writer`) and an `IDbTarget` (`db`). The writer dispatches the generated interaction; the db provides before/after row-count snapshots.

### `AssertNoPartialWritesOnErrorAsync`

Captures the row count (via `rowCount`) before executing `operation` through the `writer`. If the interaction throws an exception and the row count changed, throws `AspireEFCoreInvariantException` reporting the before/after counts — a partial write was detected. If the interaction succeeds (no exception), the assertion is skipped.

```csharp
AspireEFCoreInvariants invariants = new(writer, ordersTarget);

await invariants.AssertNoPartialWritesOnErrorAsync(
    operation: httpInteraction,
    rowCount: ctx => ctx.Set<Order>().CountAsync());
```

### `AssertIdempotentAsync`

Executes `operation` twice through the `writer`. After the second call, polls (via `IDbTarget.WaitForAsync`) until the row count converges back to the count after the first call, or until `eventualTimeout` elapses. Throws `AspireEFCoreInvariantException` if convergence does not occur.

`eventualTimeout` is required because out-of-process writes are asynchronous by default.

```csharp
await invariants.AssertIdempotentAsync(
    operation: httpInteraction,
    rowCount: ctx => ctx.Set<Order>().CountAsync(),
    eventualTimeout: TimeSpan.FromSeconds(5));
```

---

## `AspireEFCoreInvariantException`

```csharp
namespace Conjecture.Aspire.EFCore;

public sealed class AspireEFCoreInvariantException : DbInvariantException
{
    public AspireEFCoreInvariantException(string message);
    public AspireEFCoreInvariantException(string message, Exception innerException);
}
```

Derives from [`DbInvariantException`](efcore.md#dbinvariantexception). Catching `DbInvariantException` handles all DB-shape failures across the EFCore stack uniformly.

---

## `DbSnapshotInteraction`

```csharp
namespace Conjecture.Aspire.EFCore;

public sealed record DbSnapshotInteraction(
    string ResourceName,
    string Label,
    Func<DbContext, Task<object?>> Capture)
    : IInteraction;
```

An `IInteraction` step that captures a labelled DB observation within an `AspireInteractionSequenceBuilder` sequence. `ResourceName` routes the step to the correct `IDbTarget`. `Label` appears in the failure trace. `Capture` is called with a freshly-resolved `DbContext`.

`DbSnapshotInteraction` values are produced by `AspireInteractionSequenceBuilder.DbSnapshot(…)` — construct them directly only for custom sequence builders.

---

## `AspireInteractionSequenceBuilder`

```csharp
namespace Conjecture.Aspire.EFCore;

public sealed class AspireInteractionSequenceBuilder
{
    public AspireInteractionSequenceBuilder Http(
        string resourceName,
        Strategy<HttpInteraction> step);

    public AspireInteractionSequenceBuilder Message(
        string resourceName,
        Strategy<MessageInteraction> step);

    public AspireInteractionSequenceBuilder DbSnapshot(
        string resourceName,
        string label,
        Func<DbContext, Task<object?>> capture);

    public Strategy<IReadOnlyList<IAddressedInteraction>> Build(
        int minSize = 1,
        int maxSize = 20);
}
```

Fluent builder that accumulates step strategies and produces a `Strategy<IReadOnlyList<IAddressedInteraction>>`. Each registered step is selected uniformly at random when the strategy generates a sequence.

- `Http(resourceName, step)` — wraps a `Strategy<HttpInteraction>`, overwriting `ResourceName` on each generated `HttpInteraction` so `CompositeInteractionTarget` can route it to the correct `HttpClient`.
- `Message(resourceName, step)` — wraps a `Strategy<MessageInteraction>` inside `AddressedMessageInteraction`.
- `DbSnapshot(resourceName, label, capture)` — appends a deterministic `DbSnapshotInteraction` step; `capture` is called with a fresh `DbContext` at execution time.
- `Build(minSize, maxSize)` — returns a strategy that generates lists of `[minSize, maxSize]` interactions picked uniformly from all registered steps.

```csharp
Strategy<IReadOnlyList<IAddressedInteraction>> sequenceStrategy =
    new AspireInteractionSequenceBuilder()
        .Http("api", Strategy.Just(new HttpInteraction("POST", "/orders", order)))
        .DbSnapshot("orders-db", "after-place",
            ctx => ctx.Set<Order>().CountAsync().ContinueWith(t => (object?)t.Result))
        .Build(minSize: 1, maxSize: 10);
```

---

## See also

- [Tutorial 12: Composite property tests for Aspire + EF Core](../tutorials/12-aspire-efcore-integration.md)
- [How-to: Set up Aspire.EFCore property testing](../how-to/setup-aspire-efcore-property-testing.md)
- [How-to: Test eventual consistency](../how-to/test-aspire-efcore-eventual-consistency.md)
- [How-to: Assert no partial writes](../how-to/test-aspire-efcore-no-partial-writes.md)
- [How-to: Assert idempotency](../how-to/test-aspire-efcore-idempotency.md)
- [How-to: Generate interaction sequences](../how-to/generate-aspire-interaction-sequences.md)
- [Explanation: Why Aspire+EFCore composite testing works](../explanation/aspire-efcore-composite-testing.md)
- [Reference: Conjecture.EFCore](efcore.md)
- [Reference: Conjecture.Aspire](aspire.md)

# Tutorial 12: Composite property tests for Aspire + EF Core

This tutorial walks you through writing your first composite property test that exercises a .NET Aspire-orchestrated service *and* the EF Core `DbContext` that service writes to. You will:

1. Add `Conjecture.Aspire.EFCore` to a test project.
2. Extend an `IAspireAppFixture` to register `AspireDbTarget<TContext>` via `AspireDbTargetRegistry`.
3. Publish a message and use `WaitForAsync` to assert the row eventually appears.
4. Write a property test that checks no partial writes on error.
5. Read a shrunk failure trace.

## Prerequisites

- .NET 10 SDK
- A working .NET Aspire app host project (`Projects.MyStore_AppHost`)
- Familiarity with property tests — see [Tutorial 1](01-your-first-property-test.md)
- Familiarity with Aspire property tests — see [Tutorial 9](09-aspire-integration.md)

## Install

```xml
<PackageReference Include="Conjecture.Aspire.EFCore" />
<PackageReference Include="Conjecture.Aspire.Xunit" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
```

> [!NOTE]
> `Conjecture.Aspire.EFCore` depends on `Conjecture.EFCore` and `Conjecture.Aspire`. It does **not** depend on `Conjecture.AspNetCore.EFCore` — the two composite packages sit at the same architectural level. See [Explanation: Why Aspire+EFCore composite testing works](../explanation/aspire-efcore-composite-testing.md) for the design rationale.

## Step 1: Define an Aspire app host with a Postgres resource

A minimal AppHost that provisions a Postgres container and wires it to a service:

```csharp
// Projects.MyStore_AppHost/Program.cs
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<PostgresDatabaseResource> ordersDb =
    builder.AddPostgres("postgres")
           .AddDatabase("orders-db");

builder.AddProject<Projects.MyStore_Api>("api")
       .WithReference(ordersDb);

builder.Build().Run();
```

The `"orders-db"` resource name is the key that `AspireDbTarget.CreateAsync` uses to resolve the connection string.

## Step 2: Define the `DbContext`

```csharp
public class Order
{
    public Guid Id { get; set; }
    public string Customer { get; set; } = "";
    public decimal Total { get; set; }
}

public class OrdersContext(DbContextOptions<OrdersContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}
```

## Step 3: Implement the fixture

`IAspireAppFixture` owns the `DistributedApplication` lifecycle. Override `ResetAsync` to invoke `AspireDbTargetRegistry.ResetAllAsync`, which calls `EnsureDeletedAsync` + `EnsureCreatedAsync` on every registered target between examples.

```csharp
using Aspire.Hosting.Testing;
using Conjecture.Aspire;
using Conjecture.Aspire.EFCore;
using Microsoft.EntityFrameworkCore;

public sealed class StoreFixture : IAspireAppFixture, IAsyncLifetime
{
    private AspireDbTargetRegistry registry = null!;
    public AspireDbTarget<OrdersContext> Orders { get; private set; } = null!;

    public async Task<DistributedApplication> StartAsync(CancellationToken ct = default)
    {
        DistributedApplicationTestingBuilder appHost =
            await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.MyStore_AppHost>(ct);

        DistributedApplication app = await appHost.BuildAsync(ct);
        await app.StartAsync(ct);

        Orders = await AspireDbTarget.CreateAsync<OrdersContext>(
            app,
            resourceName: "orders-db",
            contextFactory: cs => new OrdersContext(
                new DbContextOptionsBuilder<OrdersContext>()
                    .UseNpgsql(cs)
                    .Options));

        registry = AspireDbTargetRegistry
            .Register(Orders);

        return app;
    }

    public Task ResetAsync(DistributedApplication app, CancellationToken ct = default)
        => registry.ResetAllAsync(ct);

    public Task InitializeAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

`AspireDbTarget.CreateAsync` calls `app.GetConnectionStringAsync("orders-db")` and passes the resolved connection string to `contextFactory`. The fixture stores `Orders` as a typed property so tests can call `Orders.Resolve()` for direct DB assertions.

## Step 4: Write a property test with eventual consistency

A service receives a `PlaceOrder` HTTP command and writes the order asynchronously. The test asserts that, within a configurable timeout, the row eventually appears.

```csharp
using Conjecture.Aspire.EFCore;
using Conjecture.Core;
using Conjecture.Xunit.V3;

public class OrderConsistencyTests(StoreFixture fixture)
    : IClassFixture<StoreFixture>
{
    [Property]
    public async Task PlacedOrder_EventuallyPersisted(Strategy<Order> orders)
    {
        Order payload = orders.Sample();

        using HttpClient client = fixture.App.CreateHttpClient("api");
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/orders", payload);
        response.EnsureSuccessStatusCode();

        // WaitForAsync polls until the row appears or the timeout expires.
        await fixture.Orders.WaitForAsync(
            predicate: ctx => ctx.Orders.AnyAsync(o => o.Id == payload.Id),
            timeout: TimeSpan.FromSeconds(5));

        OrdersContext db = fixture.Orders.Resolve();
        Order? persisted = await db.Orders.FindAsync(payload.Id);
        Assert.NotNull(persisted);
        Assert.Equal(payload.Customer, persisted.Customer);
    }
}
```

`WaitForAsync` uses a 50 ms → 250 ms exponential back-off by default. Pass `pollInterval` to override.

## Step 5: Assert no partial writes on error

`AspireEFCoreInvariants` snapshots the database before and after a generated request and fails if an error response leaves persisted state.

```csharp
using Conjecture.Aspire.EFCore;
using Conjecture.Core;
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

        await invariants.AssertNoPartialWritesOnErrorAsync(
            (client, ct) => client.PostAsJsonAsync("/orders", payload, ct));
    }
}
```

## Step 6: Read a shrunk failure trace

When `AssertNoPartialWritesOnErrorAsync` fails, Conjecture shrinks the counterexample and reports:

```text
Conjecture.Aspire.EFCore.AspireEFCoreInvariantException:
Endpoint POST /orders returned 500 but persisted 1 row(s).
Order: +1 (added [e8d8b9c2-…])

Counterexample:
  Order { Id = e8d8b9c2-…, Customer = "", Total = -0.01 }
```

The shrunk payload is the smallest `Order` that causes the endpoint to return 500 while leaking a row. Reading the trace:

- The `Total = -0.01` is the minimal value that triggers the negative-total validation path.
- The `+1` diff shows exactly one `Order` row was added despite the error.
- The trace is deterministically reproducible via `[Replay]` — paste the seed to re-run the exact failing example.

## What you have now

- A fixture that provisions a real Postgres container via Aspire, wires `AspireDbTarget<OrdersContext>`, and resets state between examples.
- An eventual-consistency assertion that polls until a row appears or a timeout expires.
- A partial-write invariant that snapshots the DB before and after every generated request.
- Failing examples that shrink to the minimal payload triggering the divergence.

## See also

- [How-to: Set up Aspire.EFCore property testing](../how-to/setup-aspire-efcore-property-testing.md)
- [How-to: Test eventual consistency](../how-to/test-aspire-efcore-eventual-consistency.md)
- [How-to: Assert no partial writes](../how-to/test-aspire-efcore-no-partial-writes.md)
- [How-to: Assert idempotency](../how-to/test-aspire-efcore-idempotency.md)
- [Reference: Conjecture.Aspire.EFCore](../reference/aspire-efcore.md)
- [Explanation: Why Aspire+EFCore composite testing works](../explanation/aspire-efcore-composite-testing.md)

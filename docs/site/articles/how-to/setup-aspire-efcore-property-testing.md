# Set up Aspire.EFCore property testing

This guide gets you from an empty test project to a passing property test that asserts EF Core persistence across a .NET Aspire-orchestrated service.

## Install

```xml
<PackageReference Include="Conjecture.Aspire.EFCore" />
<PackageReference Include="Conjecture.Aspire.Xunit" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
```

> [!NOTE]
> Replace `Npgsql.EntityFrameworkCore.PostgreSQL` with your provider package (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Sqlite`, etc.). The `AspireDbTarget` factory pattern is provider-agnostic.

## Step 1: Reference your AppHost project

```xml
<ItemGroup>
  <ProjectReference Include="..\MyStore.AppHost\MyStore.AppHost.csproj">
    <IsAspireHost>true</IsAspireHost>
  </ProjectReference>
</ItemGroup>
```

`IsAspireHost` tells the SDK to copy the Aspire manifest into the test output. This is the same reference required by `Aspire.Hosting.Testing`.

## Step 2: Create `AspireDbTarget<TContext>` via factory

Call `AspireDbTarget.CreateAsync` after the `DistributedApplication` is started. Pass the Aspire resource name and a factory function that receives the resolved connection string:

```csharp
using Aspire.Hosting.Testing;
using Conjecture.Aspire.EFCore;
using Microsoft.EntityFrameworkCore;

DistributedApplicationTestingBuilder appHost =
    await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.MyStore_AppHost>(ct);

DistributedApplication app = await appHost.BuildAsync(ct);
await app.StartAsync(ct);

AspireDbTarget<OrdersContext> ordersTarget =
    await AspireDbTarget.CreateAsync<OrdersContext>(
        app,
        resourceName: "orders-db",
        contextFactory: cs => new OrdersContext(
            new DbContextOptionsBuilder<OrdersContext>()
                .UseNpgsql(cs)
                .Options));
```

The factory receives the Aspire-resolved connection string from `app.GetConnectionStringAsync("orders-db")`. Aspire waits for the resource to be healthy before returning the connection string, so `CreateAsync` implicitly waits for the container to be ready.

> [!TIP]
> Each Aspire DB resource gets its own `AspireDbTarget<TContext>`. Multi-database apps construct one target per resource and compose them via `CompositeInteractionTarget`.

## Step 3: Register targets and wire fixture reset

Use `AspireDbTargetRegistry` to collect all targets so they can be reset between examples:

```csharp
using Conjecture.Aspire.EFCore;

AspireDbTargetRegistry registry = new();
registry.Register(ordersTarget);

// Reset between examples — call from IAspireAppFixture.ResetAsync
await registry.ResetAllAsync(ct);
```

`ResetAllAsync` calls `EnsureDeletedAsync` + `EnsureCreatedAsync` on every registered target. This drops the schema and recreates it, giving each property example a clean slate.

A complete fixture:

```csharp
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Conjecture.Aspire;
using Conjecture.Aspire.EFCore;
using Microsoft.EntityFrameworkCore;

public sealed class StoreFixture : IAspireAppFixture, IAsyncLifetime
{
    private AspireDbTargetRegistry registry = null!;
    public AspireDbTarget<OrdersContext> Orders { get; private set; } = null!;
    public DistributedApplication App { get; private set; } = null!;

    public async Task<DistributedApplication> StartAsync(CancellationToken ct = default)
    {
        DistributedApplicationTestingBuilder appHost =
            await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.MyStore_AppHost>(ct);

        App = await appHost.BuildAsync(ct);
        await App.StartAsync(ct);

        Orders = await AspireDbTarget.CreateAsync<OrdersContext>(
            App,
            resourceName: "orders-db",
            contextFactory: cs => new OrdersContext(
                new DbContextOptionsBuilder<OrdersContext>()
                    .UseNpgsql(cs)
                    .Options));

        registry = new AspireDbTargetRegistry();
        registry.Register(Orders);
        return App;
    }

    public Task ResetAsync(DistributedApplication app, CancellationToken ct = default)
        => registry.ResetAllAsync(ct);

    public Task InitializeAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

## Step 4: Write a property test

```csharp
using Conjecture.Aspire.EFCore;
using Conjecture.Core;
using Conjecture.Xunit.V3;

public class OrderTests(StoreFixture fixture) : IClassFixture<StoreFixture>
{
    [Property]
    public async Task PlacedOrder_EventuallyPersisted(Strategy<Order> orders)
    {
        Order payload = orders.Sample();

        using HttpClient client = fixture.App.CreateHttpClient("api");
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/orders", payload);
        response.EnsureSuccessStatusCode();

        await fixture.Orders.WaitForAsync(
            predicate: ctx => ctx.Orders.AnyAsync(o => o.Id == payload.Id),
            timeout: TimeSpan.FromSeconds(5));

        OrdersContext db = fixture.Orders.Resolve();
        Order? persisted = await db.Orders.FindAsync(payload.Id);
        Assert.NotNull(persisted);
    }
}
```

## Multi-database setup

For apps with more than one registered database, register each resource:

```csharp
AspireDbTarget<OrdersContext> orders = await AspireDbTarget.CreateAsync<OrdersContext>(
    app, "orders-db", cs => new OrdersContext(...));

AspireDbTarget<CatalogContext> catalog = await AspireDbTarget.CreateAsync<CatalogContext>(
    app, "catalog-db", cs => new CatalogContext(...));

AspireDbTargetRegistry registry = new();
registry.Register(orders)
        .Register(catalog);

// In ResetAsync:
await registry.ResetAllAsync(ct);
```

Both targets reset atomically between examples, so property tests that exercise cross-database flows start from a clean state.

## See also

- [Tutorial 12: Composite property tests for Aspire + EF Core](../tutorials/12-aspire-efcore-integration.md)
- [How-to: Test eventual consistency](test-aspire-efcore-eventual-consistency.md)
- [Reference: Conjecture.Aspire.EFCore](../reference/aspire-efcore.md)

# Tutorial 11: Composite property tests for ASP.NET Core + EF Core

This tutorial walks you through writing your first composite property test that exercises an ASP.NET Core endpoint *and* the EF Core `DbContext` it writes to. You will:

1. Add `Conjecture.AspNetCore.EFCore` to a test project.
2. Build a `WebApplicationFactory<TestApp>` registering a SQLite-in-memory `DbContext` and a flaky endpoint.
3. Wire `HostHttpTarget` and `AspNetCoreDbTarget<TContext>` from a single shared `IHost`.
4. Write a property test that asserts no partial writes on error responses.
5. Read a shrunk failure trace.

## Prerequisites

- .NET 10 SDK
- Familiarity with property tests — see [Tutorial 1](01-your-first-property-test.md)
- Familiarity with EF Core property tests — see [Tutorial 10](10-efcore-integration.md)

## Install

```xml
<PackageReference Include="Conjecture.AspNetCore.EFCore" />
<PackageReference Include="Conjecture.Xunit" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
```

## Step 1: Define a domain and a flaky endpoint

A trivial `Order` aggregate with a write endpoint that occasionally returns 500 *after* it has called `Add`. The shape of a real-world bug: a missed `await`, a swallowed exception, or a transaction that didn't commit.

```csharp
public class Order
{
    public Guid Id { get; set; }
    public string Customer { get; set; } = "";
    public decimal Total { get; set; }
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}
```

Minimal `Program.cs` for the test app:

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder();

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite("DataSource=:memory:"));

WebApplication app = builder.Build();

app.MapPost("/orders", async (Order order, AppDbContext db, CancellationToken ct) =>
{
    db.Orders.Add(order);
    if (order.Total < 0)
    {
        // Bug: row already added to the change tracker; SaveChangesAsync will persist it.
        return Results.StatusCode(500);
    }
    await db.SaveChangesAsync(ct);
    return Results.Created($"/orders/{order.Id}", order);
});

app.Run();

public partial class Program { }
```

The bug: the 500 path doesn't roll back the `Add`. If anything else in the request flow calls `SaveChanges`, the negative-total order persists despite the error response.

## Step 2: Build the test fixture

Open the SQLite connection once for the fixture's lifetime. Closing it drops the in-memory database; reopening every test would reset state mid-test.

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class AppFixture : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection;

    public AppFixture()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        });
        builder.ConfigureServices(services =>
        {
            using IServiceScope scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        connection.Dispose();
        base.Dispose(disposing);
    }
}
```

## Step 3: Wire `HostHttpTarget` + `AspNetCoreDbTarget<TContext>`

Both targets pull from the same `IHost` so before/after DB snapshots correlate with the HTTP response that triggered them.

# [xUnit v3](#tab/xunit-v3)

```csharp
using Conjecture.AspNetCore.EFCore;
using Conjecture.Http;
using Conjecture.Xunit.V3;

public class OrderEndpointTests : IClassFixture<AppFixture>, IAsyncDisposable
{
    private readonly HostHttpTarget http;
    private readonly AspNetCoreDbTarget<AppDbContext> db;
    private readonly AspNetCoreEFCoreInvariants invariants;

    public OrderEndpointTests(AppFixture factory)
    {
        IHost host = factory.Services.GetRequiredService<IHost>();
        HttpClient client = factory.CreateClient();

        this.http = new HostHttpTarget(host, client);
        this.db = new AspNetCoreDbTarget<AppDbContext>(host, "orders-db");
        this.invariants = new AspNetCoreEFCoreInvariants(http, db);
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        await http.DisposeAsync();
    }
}
```

# [NUnit](#tab/nunit)

```csharp
using Conjecture.AspNetCore.EFCore;
using Conjecture.Http;
using Conjecture.NUnit;

public class OrderEndpointTests
{
    private AppFixture factory = null!;
    private HostHttpTarget http = null!;
    private AspNetCoreDbTarget<AppDbContext> db = null!;
    private AspNetCoreEFCoreInvariants invariants = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        factory = new AppFixture();
        IHost host = factory.Services.GetRequiredService<IHost>();
        http = new HostHttpTarget(host, factory.CreateClient());
        db = new AspNetCoreDbTarget<AppDbContext>(host, "orders-db");
        invariants = new AspNetCoreEFCoreInvariants(http, db);
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        await db.DisposeAsync();
        await http.DisposeAsync();
        factory.Dispose();
    }
}
```

# [MSTest](#tab/mstest)

```csharp
using Conjecture.AspNetCore.EFCore;
using Conjecture.Http;
using Conjecture.MSTest;

[TestClass]
public class OrderEndpointTests
{
    private static AppFixture factory = null!;
    private static HostHttpTarget http = null!;
    private static AspNetCoreDbTarget<AppDbContext> db = null!;
    private static AspNetCoreEFCoreInvariants invariants = null!;

    [ClassInitialize]
    public static void Setup(TestContext _)
    {
        factory = new AppFixture();
        IHost host = factory.Services.GetRequiredService<IHost>();
        http = new HostHttpTarget(host, factory.CreateClient());
        db = new AspNetCoreDbTarget<AppDbContext>(host, "orders-db");
        invariants = new AspNetCoreEFCoreInvariants(http, db);
    }

    [ClassCleanup]
    public static async Task Teardown()
    {
        await db.DisposeAsync();
        await http.DisposeAsync();
        factory.Dispose();
    }
}
```

***

## Step 4: Write the property test

Generate `Order` payloads, post them, assert no partial writes on error responses.

```csharp
using Conjecture.Core;
using Conjecture.EFCore;

[Property]
public async Task PostOrders_NeverPartialWritesOnError(Strategy<Order> orders)
{
    Order payload = orders.Sample();

    await invariants.AssertNoPartialWritesOnErrorAsync(
        (client, ct) => client.PostAsJsonAsync("/orders", payload, ct));
}
```

`Generate.Entity<Order>(db.Resolve)` is registered via the `Conjecture.EFCore` strategy provider; the bound `Strategy<Order>` honours the `IModel`'s nullability and `MaxLength`/`Precision`/`Scale` constraints.

## Step 5: Read the failure

Run the test. Conjecture quickly stumbles on a negative `Total`:

```text
Falsified after 14 examples.
Shrunk to:

  Order { Id = e8d8…, Customer = "", Total = -1 }
    POST /orders → 500 Internal Server Error
    DB diff: Order: +1 (added [e8d8…])

  Conjecture.AspNetCore.EFCore.AspNetCoreEFCoreInvariantException:
  Endpoint POST /orders returned 500 but persisted 1 row(s).
  Order: +1 (added [e8d8…])
```

The shrunk counterexample reveals exactly the bug: a 500 response shouldn't have left an `Order` row behind. Fix the endpoint (e.g. `db.Orders.Remove(order)` before returning 500, or wrap in a transaction that rolls back), and the property passes.

## What you have now

- One `WebApplicationFactory` backs both an `IHttpTarget` and an `IDbTarget`.
- Generated requests run through the real ASP.NET Core pipeline; their before/after DB state is snapshotted via `EntitySnapshotter` from `Conjecture.EFCore`.
- A failing example shrinks to a minimal payload that reproduces the bug.

## Where to go next

- [How-to: Assert cascade correctness](../how-to/test-aspnetcore-efcore-cascades.md)
- [How-to: Assert endpoint idempotency](../how-to/test-aspnetcore-efcore-idempotency.md)
- [Reference: Conjecture.AspNetCore.EFCore](../reference/aspnetcore-efcore.md)
- [Explanation: Why composite HTTP+DB invariants find bugs](../explanation/aspnetcore-efcore-composite-testing.md)

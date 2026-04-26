# Reset application state between Aspire property examples

When a property test runs multiple examples against an Aspire app, each example needs a clean slate — otherwise state left by example N leaks into example N+1. This guide shows the common patterns for `ResetAsync`.

## What `ResetAsync` does

`ResetAsync(DistributedApplication app, CancellationToken ct)` is called by `AspirePropertyRunner` before each example *except the first*. The app is already running when `ResetAsync` fires; you are not restarting containers, only restoring observable state.

## Pattern 1: Admin reset endpoint

The simplest approach: your AppHost exposes a dedicated reset endpoint in its test configuration.

```csharp
public class MyAppFixture : IAspireAppFixture
{
    public override Task<DistributedApplication> StartAsync(CancellationToken ct = default)
    {
        // ... build and start
    }

    public override async Task ResetAsync(DistributedApplication app, CancellationToken ct = default)
    {
        using HttpClient client = app.CreateHttpClient("my-api");
        HttpResponseMessage response = await client.DeleteAsync("/test/reset", ct);
        response.EnsureSuccessStatusCode();
    }
}
```

The endpoint runs in the app's test configuration and can truncate EF Core tables, clear in-memory caches, or reset any other service-side state.

## Pattern 2: Database truncation via direct connection

For SQL databases, connect directly and truncate the tables you care about.

```csharp
public override async Task ResetAsync(DistributedApplication app, CancellationToken ct = default)
{
    string connectionString = await app.GetConnectionStringAsync("postgres-db", ct)
        ?? throw new InvalidOperationException("No connection string for postgres-db");

    await using NpgsqlConnection connection = new(connectionString);
    await connection.OpenAsync(ct);

    await using NpgsqlCommand cmd = new(
        "TRUNCATE orders, order_items, payments RESTART IDENTITY CASCADE",
        connection);
    await cmd.ExecuteNonQueryAsync(ct);
}
```

> [!TIP]
> `app.GetConnectionStringAsync(resourceName)` resolves the dynamic connection string that Aspire assigned at startup. Never hardcode the port.

## Pattern 3: Queue purge

For message-queue-backed services, drain the queues before each example.

```csharp
public override async Task ResetAsync(DistributedApplication app, CancellationToken ct = default)
{
    string connectionString = await app.GetConnectionStringAsync("service-bus", ct)
        ?? throw new InvalidOperationException();

    ServiceBusAdministrationClient admin = new(connectionString);
    await admin.PurgeQueueAsync("orders", ct);
    await admin.PurgeQueueAsync("payments", ct);
}
```

## Pattern 4: Combined reset

Most realistic apps need both a database truncation and a cache flush.

```csharp
public override async Task ResetAsync(DistributedApplication app, CancellationToken ct = default)
{
    await Task.WhenAll(
        TruncateDatabaseAsync(app, ct),
        FlushCacheAsync(app, ct));
}

private static async Task TruncateDatabaseAsync(DistributedApplication app, CancellationToken ct)
{
    // ...
}

private static async Task FlushCacheAsync(DistributedApplication app, CancellationToken ct)
{
    string redisEndpoint = await app.GetConnectionStringAsync("cache", ct)
        ?? throw new InvalidOperationException();
    // ...
}
```

## What NOT to reset

- **Container state** — do not call `DistributedApplication.StopAsync`; this defeats the shared-lifecycle model and makes tests 10–100x slower.
- **Aspire resource configuration** — resource names, ports, and connection strings are fixed at startup; resetting them has no effect.
- **Immutable seed data** — if your app has lookup tables that never change, don't truncate them; truncating and re-inserting large seed datasets is expensive and unnecessary.

## See also

- [Configure retry policy for flaky containers](configure-aspire-retry.md)
- [Reference: IAspireAppFixture](../reference/aspire.md)

# Conjecture.Aspire.EFCore

Bridge between [`Conjecture.Aspire`](https://www.nuget.org/packages/Conjecture.Aspire) and [`Conjecture.EFCore`](https://www.nuget.org/packages/Conjecture.EFCore). `AspireDbTarget<TContext>` resolves an EF Core `DbContext` against an Aspire-hosted database resource (Postgres, SQL Server, or any container with a connection string); `AspireDbTargetRegistry` plugs reset hooks into the fixture lifecycle so each property iteration starts from a clean schema. `AspireEFCoreInvariants` and `AspireInteractionSequenceBuilder` enforce cross-service correctness — no partial writes on error, idempotent retries, and HTTP+message+DB-snapshot interleaving.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Aspire
dotnet add package Conjecture.EFCore
dotnet add package Conjecture.Aspire.EFCore
```

## Usage

```csharp
using Conjecture.Aspire.EFCore;
using Conjecture.EFCore;
using Aspire.Hosting;

DistributedApplication app = await DistributedApplication.CreateBuilder().BuildAsync();

await using AspireDbTarget<MyDbContext> orders = await AspireDbTarget<MyDbContext>.CreateAsync(
    app,
    resourceName: "orders-db",
    contextFactory: connStr =>
        new MyDbContext(new DbContextOptionsBuilder<MyDbContext>().UseNpgsql(connStr).Options));

// Run an HTTP + DbSnapshot interleaved sequence
Strategy<IReadOnlyList<IAddressedInteraction>> sequence = new AspireInteractionSequenceBuilder()
    .Http("api", Generate.Http("api").Post("/orders", new { sku = "A" }).Build())
    .DbSnapshot("orders-db", "after-post", async ctx => await ctx.Set<Order>().CountAsync())
    .Build(minSize: 1, maxSize: 5);
```

For invariants:

```csharp
AspireEFCoreInvariants invariants = new(httpTarget, orders);
await invariants.AssertNoPartialWritesOnErrorAsync(
    new HttpInteraction("api", "POST", "/orders", new { sku = "" }, null),
    rowCount: ctx => ctx.Set<Order>().CountAsync());
```

## Types

| Type | Role |
|---|---|
| `AspireDbTarget<TContext>` | `IDbTarget` over an Aspire-hosted DB resource. `CreateAsync(app, resourceName, factory)` resolves the connection string and constructs the context. |
| `AspireDbTargetRegistry` | Collection of registered `IDbTarget`s with shared `ResetAllAsync`. |
| `AspireDbFixtureExtensions.CreateDbRegistry(this fixture)` | Wires reset hooks into the fixture lifecycle. |
| `AspireEFCoreInvariants` | `AssertNoPartialWritesOnErrorAsync`, `AssertIdempotentAsync`. |
| `AspireInteractionSequenceBuilder` | Composes interleaved HTTP + message + DB-snapshot sequences. |
| `DbSnapshotInteraction` | Captures a DB read inside an interaction sequence. |
| `IDbTargetWaitForExtensions.WaitForAsync` | Polls until a predicate over the DB context becomes true. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)

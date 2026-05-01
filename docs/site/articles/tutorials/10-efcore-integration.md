# Tutorial 10: Property tests for EF Core

This tutorial walks you through writing your first EF Core property test end to end. You will:

1. Add `Conjecture.EFCore` to a test project.
2. Configure a SQLite-in-memory `DbContext` factory.
3. Write a `RoundtripAsserter.AssertRoundtripAsync` property test.
4. Add migration up/down assertions with `MigrationHarness`.
5. Customise the strategy via `EntityStrategyBuilder`.

## Prerequisites

- .NET 10 SDK
- A test project referencing `Conjecture.Xunit`, `Conjecture.NUnit`, `Conjecture.MSTest`, or `Conjecture.TestingPlatform`
- Familiarity with `[Property]` attributes — see [Tutorial 1](01-your-first-property-test.md) if not

## Install

```xml
<PackageReference Include="Conjecture.EFCore" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
```

## Step 1: Define a domain

A small domain with one aggregate root, one owned type, and one navigation. EF will derive an `IModel` from this — Conjecture will derive entity strategies from that model.

```csharp
using Microsoft.EntityFrameworkCore;

public class Order
{
    public Guid Id { get; set; }
    public string Customer { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime PlacedAt { get; set; }
    public Address ShippingAddress { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>(o =>
        {
            o.HasKey(x => x.Id);
            o.Property(x => x.Customer).HasMaxLength(100).IsRequired();
            o.Property(x => x.Total).HasPrecision(18, 2);
            o.OwnsOne(x => x.ShippingAddress);
        });
    }
}
```

## Step 2: Wire a SQLite-in-memory factory

A shared in-memory connection so every `DbContext` instance the test creates sees the same schema. Open the connection once for the test class's lifetime — closing it drops the database.

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public sealed class SqliteAppDbContextFactory : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<AppDbContext> options;

    public SqliteAppDbContextFactory()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        using AppDbContext bootstrap = new AppDbContext(options);
        bootstrap.Database.EnsureCreated();
    }

    public AppDbContext Create() => new AppDbContext(options);

    public ValueTask DisposeAsync()
    {
        connection.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

## Step 3: Write the roundtrip property

The first property: every generated `Order` survives `SaveChanges` + reload from a fresh context with no observable change.

# [xUnit v3](#tab/xunit-v3)

```csharp
using Conjecture.Core;
using Conjecture.EFCore;
using Conjecture.Xunit.V3;

public class OrderRoundtripTests : IAsyncDisposable
{
    private readonly SqliteAppDbContextFactory factory = new();

    [Property]
    public async Task Order_Saves_And_Reloads_Without_Loss()
    {
        Strategy<Order> orders = Strategy.Entity<Order>(factory.Create);
        Order order = orders.Sample();

        await RoundtripAsserter.AssertRoundtripAsync(factory.Create, order);
    }

    public ValueTask DisposeAsync() => factory.DisposeAsync();
}
```

# [NUnit](#tab/nunit)

```csharp
using Conjecture.Core;
using Conjecture.EFCore;
using Conjecture.NUnit;

public class OrderRoundtripTests
{
    private SqliteAppDbContextFactory factory = null!;

    [SetUp] public void Setup() => factory = new SqliteAppDbContextFactory();
    [TearDown] public async Task Teardown() => await factory.DisposeAsync();

    [Property]
    public async Task Order_Saves_And_Reloads_Without_Loss()
    {
        Strategy<Order> orders = Strategy.Entity<Order>(factory.Create);
        Order order = orders.Sample();

        await RoundtripAsserter.AssertRoundtripAsync(factory.Create, order);
    }
}
```

# [MSTest](#tab/mstest)

```csharp
using Conjecture.Core;
using Conjecture.EFCore;
using Conjecture.MSTest;

[TestClass]
public class OrderRoundtripTests
{
    private SqliteAppDbContextFactory factory = null!;

    [TestInitialize] public void Init() => factory = new SqliteAppDbContextFactory();
    [TestCleanup] public async Task Cleanup() => await factory.DisposeAsync();

    [Property]
    public async Task Order_Saves_And_Reloads_Without_Loss()
    {
        Strategy<Order> orders = Strategy.Entity<Order>(factory.Create);
        Order order = orders.Sample();

        await RoundtripAsserter.AssertRoundtripAsync(factory.Create, order);
    }
}
```

***

Run it. Each example draws a structurally-valid `Order` (honouring `MaxLength(100)` on `Customer`, `Precision(18, 2)` on `Total`, and an inline `ShippingAddress`), saves it, reloads it from a fresh context, and reports the first mismatched scalar property if any.

If the property passes, you've ruled out value-converter precision loss, missing-property mappings, and required-property nullability mismatches across the entire generated entity space.

## Step 4: Catch a deliberate bug

To see what failure looks like, add a value converter that silently truncates `Order.PlacedAt` to seconds:

```csharp
o.Property(x => x.PlacedAt).HasConversion(
    d => new DateTime(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, d.Kind),
    d => d);
```

Re-run. The property fails:

```text
Conjecture.EFCore.RoundtripAssertionException:
Roundtrip assertion failed (roundtrip reload):
  Property 'PlacedAt': expected '2026-04-27T14:23:11.4567890', got '2026-04-27T14:23:11.0000000'.
```

Conjecture shrinks down to a minimal `Order` with `PlacedAt` carrying sub-second precision. Remove the converter, the property passes again.

## Step 5: Test migrations

Add a migration to the project (`dotnet ef migrations add InitialCreate`) and call the harness:

```csharp
[Property]
public async Task Migrations_RoundTrip()
{
    await using SqliteAppDbContextFactory factory = new();
    await using AppDbContext db = factory.Create();
    await MigrationHarness.AssertUpDownIdempotentAsync(db);
}
```

The harness applies migrations to head, snapshots `sqlite_master`, runs the latest migration's `Down`, re-applies its `Up`, and throws `MigrationAssertionException` if the schema drifts. See [the migrations how-to](../how-to/test-efcore-migrations.md) for failure-mode patterns.

## Step 6: Customise the strategy

Suppress the `ShippingAddress.Street` requirement, or drop the navigation entirely:

```csharp
EntityStrategyBuilder b = new EntityStrategyBuilder(db.Model)
    .WithMaxDepth(1)                                    // shallow graphs
    .WithoutNavigation<Order>(o => o.ShippingAddress);  // null out the address

Strategy<Order> minimalOrders = b.Build<Order>();
```

`Strategy.Entity<T>` is a thin wrapper over `EntityStrategyBuilder`; reach for the builder when you need `WithoutNavigation` or a non-default `WithMaxDepth`. See [customise entity generation](../how-to/customise-efcore-entity-generation.md) for the broader pattern.

## What you have now

- One property test that asserts roundtrip integrity over the entire structurally-valid entity space.
- A migration harness that catches asymmetric `Down` migrations.
- A clear path to drop into `EntityStrategyBuilder` when defaults aren't sufficient.

## Where to go next

- [How-to: Customise EF Core entity generation](../how-to/customise-efcore-entity-generation.md)
- [How-to: Test migration up/down invariants](../how-to/test-efcore-migrations.md)
- [Reference: Conjecture.EFCore](../reference/efcore.md)
- [Explanation: Why property testing finds EF Core bugs](../explanation/efcore-property-testing.md)

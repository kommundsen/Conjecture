# Set up EF Core property testing

This guide gets you from an empty test project to a passing roundtrip property test against a SQLite-backed `DbContext`.

## Install

```xml
<PackageReference Include="Conjecture.EFCore" />
<PackageReference Include="Conjecture.Xunit" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
```

> [!NOTE]
> SQLite (in-memory) is the recommended default test provider per [ADR 0065](../../decisions/0065-conjecture-efcore-package-design.md). Any provider that produces a valid `IModel` works for entity strategies; SQLite gives you real SQL and migrations without external infrastructure.

## Step 1: Define your `DbContext`

A minimal context. Keep entity types public so EF reflection can discover them.

```csharp
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}

public class Order
{
    public Guid Id { get; set; }
    public string Customer { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime PlacedAt { get; set; }
}
```

## Step 2: Wire a SQLite-in-memory factory

A shared in-memory connection so every `DbContext` instance the test creates sees the same schema.

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
        options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

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

> [!TIP]
> Open the connection once and keep it open for the test class's lifetime. Closing the connection drops the in-memory database.

## Step 3: Write your first roundtrip property test

Use `RoundtripAsserter.AssertRoundtripAsync` to assert that any generated `Order` survives `SaveChanges` + reload without observable change.

# [xUnit v3](#tab/xunit-v3)

```csharp
using Conjecture.Core;
using Conjecture.EFCore;
using Conjecture.Xunit.V3;

public class OrderRoundtripTests : IAsyncDisposable
{
    private readonly SqliteAppDbContextFactory factory = new();

    [Property]
    public async Task Order_Roundtrips_Without_Loss(Strategy<Order> orders)
    {
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
    public async Task Order_Roundtrips_Without_Loss(Strategy<Order> orders)
    {
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
    public async Task Order_Roundtrips_Without_Loss(Strategy<Order> orders)
    {
        Order order = orders.Sample();
        await RoundtripAsserter.AssertRoundtripAsync(factory.Create, order);
    }
}
```

***

When the property runs, Conjecture draws a fresh `Order` per example, saves it, and reloads it from a new `DbContext`. The default comparer walks scalar properties and reports the offending field on mismatch.

## Step 4: Bind the strategy from the model

The `Strategy<Order>` parameter above is resolved from the registered `Generate.For<Order>()` provider. Wire it from your `DbContext` model:

```csharp
[ConjectureSettings]
public class AssemblyFixture
{
    public AssemblyFixture()
    {
        // Register before the first test fires.
        using SqliteAppDbContextFactory bootstrap = new();
        using AppDbContext db = bootstrap.Create();
        GenerateForRegistry.Register(typeof(Order), () => new ModelStrategyProvider(db.Model));
    }
}

internal sealed class ModelStrategyProvider(IModel model) : IStrategyProvider
{
    public Strategy<T> Get<T>() where T : class => Generate.Entity<T>(() =>
    {
        // Build from the cached IModel without instantiating a real context per draw
        DbContextOptions opts = new DbContextOptionsBuilder().UseSqlite("DataSource=:memory:").Options;
        return new DbContext(opts);
    });
}
```

For the simpler case — explicit factory inside the test method — call `Generate.Entity<Order>(factory.Create)` directly:

```csharp
[Property]
public async Task Order_Roundtrips_Without_Loss()
{
    Strategy<Order> orders = Generate.Entity<Order>(factory.Create);
    Order order = orders.Sample();
    await RoundtripAsserter.AssertRoundtripAsync(factory.Create, order);
}
```

## What you have now

- Each property example draws a structurally-valid `Order` honouring `IProperty` constraints (nullable, `MaxLength`, `Precision`/`Scale`).
- `RoundtripAsserter` saves, reloads from a fresh context, and reports the first mismatched scalar property.
- Failure shrinks down to the smallest counterexample that still triggers the divergence.

## See also

- [How-to: Customise entity generation](customise-efcore-entity-generation.md)
- [How-to: Test migration up/down invariants](test-efcore-migrations.md)
- [Reference: Conjecture.EFCore](../reference/efcore.md)

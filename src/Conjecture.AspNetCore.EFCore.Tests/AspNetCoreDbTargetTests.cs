// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading.Tasks;

using Conjecture.EFCore;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.AspNetCore.EFCore.Tests;

public sealed class TestApp
{
}

public sealed class AspNetCoreDbTargetFixture : IDisposable
{
    private readonly SqliteConnection connection;

    public WebApplicationFactory<TestApp> Factory { get; }

    public AspNetCoreDbTargetFixture()
    {
        connection = new("DataSource=:memory:");
        connection.Open();

        Factory = new WebApplicationFactory<TestApp>().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddDbContext<OrdersDbContext>(opts =>
                    opts.UseSqlite(connection))));

        using IServiceScope scope = Factory.Services.CreateScope();
        OrdersDbContext ctx = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Factory.Dispose();
        connection.Dispose();
    }

    internal sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> opts) : DbContext(opts)
    {
        public DbSet<Order> Orders => Set<Order>();
    }

    internal sealed class Order
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    internal sealed class UnrelatedDbContext(DbContextOptions<UnrelatedDbContext> opts) : DbContext(opts)
    {
    }
}

public sealed class AspNetCoreDbTargetTests(AspNetCoreDbTargetFixture fixture) : IClassFixture<AspNetCoreDbTargetFixture>
{
    private readonly AspNetCoreDbTargetFixture fixture = fixture;

    private IHost Host => fixture.Factory.Services.GetRequiredService<IHost>();

    [Fact]
    public void IsIDbTarget()
    {
        bool assignable = typeof(IDbTarget).IsAssignableFrom(typeof(AspNetCoreDbTarget<DbContext>));
        Assert.True(assignable);
    }

    [Fact]
    public void ResourceName_IsSetFromConstructor()
    {
        AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext> target = new(Host, "orders-db");
        Assert.Equal("orders-db", target.ResourceName);
    }

    [Fact]
    public void ResolveContext_ReturnsScopedInstance_FreshPerCall()
    {
        AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext> target = new(Host, "orders-db");

        DbContext c1 = target.ResolveContext("orders-db");
        DbContext c2 = target.ResolveContext("orders-db");

        try
        {
            Assert.False(ReferenceEquals(c1, c2));
            c1.Dispose();
            Assert.False(c2.ChangeTracker.HasChanges());
        }
        finally
        {
            c1.Dispose();
            c2.Dispose();
        }
    }

    [Fact]
    public void Resolve_TypedOverload_ReturnsTContext()
    {
        AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext> target = new(Host, "orders-db");
        AspNetCoreDbTargetFixture.OrdersDbContext ctx = target.Resolve();
        try
        {
            Assert.IsType<AspNetCoreDbTargetFixture.OrdersDbContext>(ctx);
        }
        finally
        {
            ctx.Dispose();
        }
    }

    [Fact]
    public void ResolveContext_LeaksNoChangeTrackerStateAcrossExamples()
    {
        AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext> target = new(Host, "orders-db");

        AspNetCoreDbTargetFixture.OrdersDbContext scopeA = target.Resolve();
        scopeA.Orders.Add(new AspNetCoreDbTargetFixture.Order());
        scopeA.Dispose();

        AspNetCoreDbTargetFixture.OrdersDbContext scopeB = target.Resolve();
        try
        {
            Assert.Empty(scopeB.Set<AspNetCoreDbTargetFixture.Order>().Local);
        }
        finally
        {
            scopeB.Dispose();
        }
    }

    [Fact]
    public void ResolveContext_UnknownResourceName_Throws()
    {
        AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext> target = new(Host, "orders-db");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => target.ResolveContext("not-orders-db"));

        Assert.Contains("not-orders-db", ex.Message);
        Assert.Contains("orders-db", ex.Message);
    }

    [Fact]
    public void Constructor_NullHost_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext>(null!, "orders-db"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_NullOrEmptyResource_Throws(string? resourceName)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext>(Host, resourceName!));
    }

    [Fact]
    public async Task ResetAsync_ClearsState()
    {
        AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext> target = new(Host, "orders-db");

        AspNetCoreDbTargetFixture.OrdersDbContext ctx = target.Resolve();
        await using (ctx)
        {
            ctx.Orders.Add(new AspNetCoreDbTargetFixture.Order { Name = "ToDelete" });
            await ctx.SaveChangesAsync();
        }

        await target.ResetAsync("orders-db", default);

        AspNetCoreDbTargetFixture.OrdersDbContext fresh = target.Resolve();
        await using (fresh)
        {
            int count = await fresh.Set<AspNetCoreDbTargetFixture.Order>().AsNoTracking().CountAsync();
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task ResetAsync_UnknownResourceName_Throws()
    {
        AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext> target = new(Host, "orders-db");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => target.ResetAsync("not-orders-db", default));
    }

    [Fact]
    public async Task ExecuteAsync_AddThenSaveChanges_DispatchesThroughScopedContext()
    {
        AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext> target = new(Host, "orders-db");

        // Per v0.26.0 contract: each ExecuteAsync opens its own DbContext scope.
        // SaveChanges on a fresh empty context returns 0 (boxed int).
        object? result = await target.ExecuteAsync(
            new DbInteraction("orders-db", DbOpKind.SaveChanges, null),
            default);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteAsync_NonDbInteraction_Throws()
    {
        AspNetCoreDbTarget<AspNetCoreDbTargetFixture.OrdersDbContext> target = new(Host, "orders-db");

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => target.ExecuteAsync(new NonDbInteraction(), default));

        Assert.Contains(nameof(NonDbInteraction), ex.Message);
    }

    private sealed class NonDbInteraction : IInteraction
    {
    }
}
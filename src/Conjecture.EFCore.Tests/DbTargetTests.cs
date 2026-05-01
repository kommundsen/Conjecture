// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.EFCore;

using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.EFCore.Tests;

public class DbTargetTests
{
    private sealed class OrderItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    }

    private static TestDbContext CreateInMemoryContext()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("orders-db")
            .Options;
        return new(opts);
    }

    private static TestDbContext CreateSqliteContext(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;
        return new(opts);
    }

    [Fact]
    public void IDbTarget_IsInteractionTarget()
    {
        Assert.True(typeof(IInteractionTarget).IsAssignableFrom(typeof(IDbTarget)));
    }

    [Fact]
    public void InMemoryDbTarget_Resolve_ReturnsContextForResource()
    {
        InMemoryDbTarget target = new("orders-db", static () =>
        {
            DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase("orders-db")
                .Options;
            return new TestDbContext(opts);
        });

        DbContext ctx = target.ResolveContext("orders-db");

        Assert.NotNull(ctx);
        Assert.True(ctx.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InMemoryDbTarget_Resolve_UnknownResource_Throws()
    {
        InMemoryDbTarget target = new("orders-db", static () =>
        {
            DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase("orders-db")
                .Options;
            return new TestDbContext(opts);
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => { target.ResolveContext("unknown"); });
        Assert.Contains("unknown", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InMemoryDbTarget_ResetAsync_ClearsState()
    {
        InMemoryDbTarget target = new("orders-db", static () =>
        {
            DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase("orders-db")
                .Options;
            return new TestDbContext(opts);
        });

        TestDbContext addCtx = (TestDbContext)target.ResolveContext("orders-db");
        addCtx.OrderItems.Add(new() { Id = 1, Name = "Widget" });
        await addCtx.SaveChangesAsync();

        await target.ResetAsync("orders-db", CancellationToken.None);

        TestDbContext freshCtx = (TestDbContext)target.ResolveContext("orders-db");
        int count = await freshCtx.OrderItems.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public void SqliteDbTarget_Resolve_ReturnsContextForResource()
    {
        using Microsoft.Data.Sqlite.SqliteConnection conn = new("Data Source=:memory:");
        conn.Open();

        SqliteDbTarget target = new("orders-db", () => CreateSqliteContext(conn));
        DbContext ctx = target.ResolveContext("orders-db");
        ctx.Database.EnsureCreated();

        Assert.NotNull(ctx);
        Assert.True(ctx.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SqliteDbTarget_ResetAsync_ClearsState()
    {
        using Microsoft.Data.Sqlite.SqliteConnection conn = new("Data Source=:memory:");
        conn.Open();

        SqliteDbTarget target = new("orders-db", () => CreateSqliteContext(conn));

        TestDbContext addCtx = (TestDbContext)target.ResolveContext("orders-db");
        addCtx.Database.EnsureCreated();
        addCtx.OrderItems.Add(new() { Id = 1, Name = "Widget" });
        await addCtx.SaveChangesAsync();

        await target.ResetAsync("orders-db", CancellationToken.None);

        TestDbContext freshCtx = (TestDbContext)target.ResolveContext("orders-db");
        int count = await freshCtx.OrderItems.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SqliteDbTarget_AsyncDispose_ClosesConnection()
    {
        Microsoft.Data.Sqlite.SqliteConnection conn = new("Data Source=:memory:");
        conn.Open();

        SqliteDbTarget target = new("orders-db", () => CreateSqliteContext(conn));
        await target.DisposeAsync();

        Assert.ThrowsAny<Exception>(() =>
        {
            DbContext ctx = target.ResolveContext("orders-db");
            ctx.Database.EnsureCreated();
        });
    }
}
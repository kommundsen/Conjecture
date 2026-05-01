// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Aspire.EFCore;
using Conjecture.EFCore;
using Conjecture.Interactions;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.EFCore.Tests;

/// <summary>
/// Tests for <see cref="AspireDbTarget{TContext}"/>.
/// Uses a SQLite in-memory connection string wired through a fake <see cref="ConnectionStringResolver"/>.
/// </summary>
public sealed class AspireDbTargetTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;

    public async Task InitializeAsync()
    {
        // Keep a single open connection — SQLite :memory: databases live only as long as the connection.
        connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<OrdersDbContext> opts = SharedOpts(connection);
        await using OrdersDbContext seed = new(opts);
        await seed.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await connection.DisposeAsync();
    }

    /// <summary>
    /// Options that share the existing open <paramref name="conn"/> so all contexts see the same schema.
    /// </summary>
    private static DbContextOptions<OrdersDbContext> SharedOpts(SqliteConnection conn) =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(conn).Options;

    private static DbContextOptions<OrdersDbContext> DbOpts(string connStr) =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(connStr).Options;

    private Func<string, OrdersDbContext> Factory() => _ => new OrdersDbContext(SharedOpts(connection));

    private ConnectionStringResolver KnownResolver() =>
        (name, _) => Task.FromResult<string?>(name == "orders-db" ? connection.ConnectionString : null);

    // -----------------------------------------------------------------------
    // CreateAsync — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_resolves_connection_string_from_distributed_application()
    {
        AspireDbTarget<OrdersDbContext> target = await AspireDbTarget<OrdersDbContext>.CreateAsync(
            KnownResolver(),
            "orders-db",
            Factory());

        await using (target)
        {
            OrdersDbContext ctx = target.Resolve();
            Assert.IsType<OrdersDbContext>(ctx);
        }
    }

    // -----------------------------------------------------------------------
    // CreateAsync — unknown resource throws InvalidOperationException
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_throws_when_resource_unknown()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AspireDbTarget<OrdersDbContext>.CreateAsync(
                KnownResolver(),
                "nonexistent-resource",
                Factory()));
    }

    // -----------------------------------------------------------------------
    // ResolveContext — name mismatch throws
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveContext_throws_when_name_mismatches()
    {
        AspireDbTarget<OrdersDbContext> target = await AspireDbTarget<OrdersDbContext>.CreateAsync(
            KnownResolver(),
            "orders-db",
            Factory());

        await using (target)
        {
            Assert.Throws<InvalidOperationException>(
                () => target.ResolveContext("other-db"));
        }
    }

    // -----------------------------------------------------------------------
    // ExecuteAsync — Add, Update, Remove, SaveChanges
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DbOpKind.Add)]
    [InlineData(DbOpKind.Update)]
    [InlineData(DbOpKind.Remove)]
    public async Task ExecuteAsync_dispatches_Add_Update_Remove_without_throwing(DbOpKind op)
    {
        AspireDbTarget<OrdersDbContext> target = await AspireDbTarget<OrdersDbContext>.CreateAsync(
            KnownResolver(),
            "orders-db",
            Factory());

        await using (target)
        {
            Order entity = new() { Id = 1, Name = "X" };
            object? result = await target.ExecuteAsync(new DbInteraction("orders-db", op, entity), default);
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_dispatches_SaveChanges_returns_int()
    {
        AspireDbTarget<OrdersDbContext> target = await AspireDbTarget<OrdersDbContext>.CreateAsync(
            KnownResolver(),
            "orders-db",
            Factory());

        await using (target)
        {
            object? result = await target.ExecuteAsync(
                new DbInteraction("orders-db", DbOpKind.SaveChanges, null),
                default);
            Assert.IsType<int>(result);
        }
    }

    // -----------------------------------------------------------------------
    // ExecuteAsync — Query throws NotSupportedException
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_throws_for_Query_op()
    {
        AspireDbTarget<OrdersDbContext> target = await AspireDbTarget<OrdersDbContext>.CreateAsync(
            KnownResolver(),
            "orders-db",
            Factory());

        await using (target)
        {
            await Assert.ThrowsAsync<NotSupportedException>(
                () => target.ExecuteAsync(new DbInteraction("orders-db", DbOpKind.Query, null), default));
        }
    }

    // -----------------------------------------------------------------------
    // ExecuteAsync — wrong interaction type throws InvalidOperationException
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_throws_for_non_DbInteraction()
    {
        AspireDbTarget<OrdersDbContext> target = await AspireDbTarget<OrdersDbContext>.CreateAsync(
            KnownResolver(),
            "orders-db",
            Factory());

        await using (target)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.ExecuteAsync(new NonDbInteraction(), default));
        }
    }

    // -----------------------------------------------------------------------
    // ResetAsync — seed data is gone after reset
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResetAsync_recreates_schema()
    {
        AspireDbTarget<OrdersDbContext> target = await AspireDbTarget<OrdersDbContext>.CreateAsync(
            KnownResolver(),
            "orders-db",
            Factory());

        await using (target)
        {
            OrdersDbContext seedCtx = target.Resolve();
            await using (seedCtx)
            {
                seedCtx.Orders.Add(new Order { Id = 99, Name = "SeedRow" });
                await seedCtx.SaveChangesAsync();
            }

            await target.ResetAsync("orders-db");

            OrdersDbContext freshCtx = target.Resolve();
            await using (freshCtx)
            {
                int count = await freshCtx.Set<Order>().AsNoTracking().CountAsync();
                Assert.Equal(0, count);
            }
        }
    }

    // -----------------------------------------------------------------------
    // DisposeAsync — disposes all tracked contexts
    // -----------------------------------------------------------------------

    private DbContextOptions<DisposeSentinelDbContext> SentinelOpts() =>
        new DbContextOptionsBuilder<DisposeSentinelDbContext>().UseSqlite(connection).Options;

    [Fact]
    public async Task DisposeAsync_disposes_all_tracked_contexts()
    {
        DisposeSentinelDbContext ctx1 = new(SentinelOpts());
        DisposeSentinelDbContext ctx2 = new(SentinelOpts());

        // Build a target with a factory that returns sentinels in order.
        int callCount = 0;
        DisposeSentinelDbContext[] sentinels = [ctx1, ctx2];
        AspireDbTarget<DisposeSentinelDbContext> target = await AspireDbTarget<DisposeSentinelDbContext>.CreateAsync(
            (name, _) => Task.FromResult<string?>(name == "orders-db" ? connection.ConnectionString : null),
            "orders-db",
            _ => sentinels[callCount++]);

        // Trigger two tracked resolutions.
        target.ResolveContext("orders-db");
        target.ResolveContext("orders-db");

        await target.DisposeAsync();

        Assert.True(ctx1.IsDisposed);
        Assert.True(ctx2.IsDisposed);
    }

    // -----------------------------------------------------------------------
    // Nested helpers and test doubles
    // -----------------------------------------------------------------------

    internal sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> opts) : DbContext(opts)
    {
        public DbSet<Order> Orders => Set<Order>();
    }

    internal sealed class Order
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    internal sealed class DisposeSentinelDbContext(DbContextOptions<DisposeSentinelDbContext> opts) : DbContext(opts)
    {
        public bool IsDisposed { get; private set; }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }

        public override ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return base.DisposeAsync();
        }
    }

    private sealed class NonDbInteraction : IInteraction
    {
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;

using Conjecture.EFCore;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conjecture.EFCore.Tests;

public class IDbTargetExtensionsTests
{
    private sealed class OrderItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class OrdersDbContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderItem>();
        }
    }

    private sealed class UnrelatedDbContext(DbContextOptions options) : DbContext(options)
    {
    }

    private sealed class TrackingOrdersDbContext(DbContextOptions options) : OrdersDbContext(options)
    {
        public bool WasDisposed { get; private set; }

        public override void Dispose()
        {
            WasDisposed = true;
            base.Dispose();
        }
    }

    private static SqliteDbTarget CreateOrdersTarget(SqliteConnection connection)
    {
        return new("orders-db", () =>
        {
            DbContextOptions<OrdersDbContext> opts = new DbContextOptionsBuilder<OrdersDbContext>()
                .UseSqlite(connection)
                .Options;
            return new OrdersDbContext(opts);
        });
    }

    [Fact]
    public void Resolve_TypedContext_ReturnsExpectedInstance()
    {
        using SqliteConnection conn = new("Data Source=:memory:");
        conn.Open();
        SqliteDbTarget target = CreateOrdersTarget(conn);

        OrdersDbContext ctx = target.Resolve<OrdersDbContext>();

        Assert.NotNull(ctx);
        Assert.Equal(typeof(OrdersDbContext), ctx.GetType());
    }

    [Fact]
    public void Resolve_WrongTypeArgument_Throws()
    {
        using SqliteConnection conn = new("Data Source=:memory:");
        conn.Open();
        SqliteDbTarget target = CreateOrdersTarget(conn);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => { target.Resolve<UnrelatedDbContext>(); });

        Assert.Contains("orders-db", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(UnrelatedDbContext), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_NullTarget_Throws()
    {
        IDbTarget? nullTarget = null;

        Assert.Throws<ArgumentNullException>(
            () => { nullTarget!.Resolve<DbContext>(); });
    }

    [Fact]
    public void Resolve_BaseDbContext_AlsoWorks()
    {
        using SqliteConnection conn = new("Data Source=:memory:");
        conn.Open();
        SqliteDbTarget target = CreateOrdersTarget(conn);

        DbContext ctx = target.Resolve<DbContext>();

        Assert.NotNull(ctx);
        Assert.True(ctx is OrdersDbContext);
    }

    [Fact]
    public void Resolve_DisposesContextOnTypeMismatch()
    {
        TrackingOrdersDbContext? produced = null;

        SqliteDbTarget target = new("orders-db", () =>
        {
            using SqliteConnection conn = new("Data Source=:memory:");
            conn.Open();
            DbContextOptions<TrackingOrdersDbContext> opts =
                new DbContextOptionsBuilder<TrackingOrdersDbContext>()
                    .UseSqlite(conn)
                    .Options;
            produced = new(opts);
            return produced;
        });

        Assert.Throws<InvalidOperationException>(
            () => { target.Resolve<UnrelatedDbContext>(); });

        Assert.NotNull(produced);
        Assert.True(produced.WasDisposed);
    }
}

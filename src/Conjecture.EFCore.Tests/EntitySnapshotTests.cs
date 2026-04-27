// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading.Tasks;

using Conjecture.EFCore;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conjecture.EFCore.Tests;

public class EntitySnapshotTests
{
    // ---- test entities ---------------------------------------------------

    private sealed class Order
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    // ---- DbContext -------------------------------------------------------

    private sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.Property(e => e.Name).IsRequired();
            });
        }
    }

    // ---- helpers ---------------------------------------------------------

    private static (SqliteConnection connection, SqliteDbTarget target) CreateTarget()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<OrderDbContext> options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlite(connection)
            .Options;

        OrderDbContext seed = new(options);
        seed.Database.EnsureCreated();
        seed.Dispose();

        SqliteDbTarget target = new("orders", () => new OrderDbContext(options));
        return (connection, target);
    }

    // ---- tests -----------------------------------------------------------

    [Fact]
    public async Task CaptureAsync_EmptyDb_ReturnsZeroCounts()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget();
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        EntitySnapshot snapshot = await EntitySnapshotter.CaptureAsync(target);

        Assert.Equal(0, snapshot.Counts[typeof(Order)]);
        Assert.Empty(snapshot.Keys[typeof(Order)]);
    }

    [Fact]
    public async Task CaptureAsync_AfterAdd_ReflectsRow()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget();
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        await using (DbContext ctx = target.ResolveContext("orders"))
        {
            ctx.Set<Order>().Add(new Order { Name = "First" });
            await ctx.SaveChangesAsync();
        }

        EntitySnapshot snapshot = await EntitySnapshotter.CaptureAsync(target);

        Assert.Equal(1, snapshot.Counts[typeof(Order)]);
        Assert.Single(snapshot.Keys[typeof(Order)]);
    }

    [Fact]
    public async Task Diff_NoChange_IsEmpty()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget();
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        EntitySnapshot before = await EntitySnapshotter.CaptureAsync(target);
        EntitySnapshot after = await EntitySnapshotter.CaptureAsync(target);

        EntitySnapshotDiff diff = EntitySnapshotter.Diff(before, after);

        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public async Task Diff_AddedRow_ReportsPositiveDelta()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget();
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        EntitySnapshot before = await EntitySnapshotter.CaptureAsync(target);

        Order added = new() { Name = "New" };
        await using (DbContext ctx = target.ResolveContext("orders"))
        {
            ctx.Set<Order>().Add(added);
            await ctx.SaveChangesAsync();
        }

        EntitySnapshot after = await EntitySnapshotter.CaptureAsync(target);

        EntitySnapshotDiff diff = EntitySnapshotter.Diff(before, after);

        Assert.Equal(1, diff.CountDeltas[typeof(Order)]);
        Assert.Contains(added.Id, diff.AddedKeys[typeof(Order)]);
        Assert.False(diff.IsEmpty);
    }

    [Fact]
    public async Task Diff_RemovedRow_ReportsNegativeDelta()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget();
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        Order existing = new() { Name = "ToRemove" };
        await using (DbContext ctx = target.ResolveContext("orders"))
        {
            ctx.Set<Order>().Add(existing);
            await ctx.SaveChangesAsync();
        }

        EntitySnapshot before = await EntitySnapshotter.CaptureAsync(target);

        await using (DbContext ctx = target.ResolveContext("orders"))
        {
            ctx.Set<Order>().Remove(existing);
            await ctx.SaveChangesAsync();
        }

        EntitySnapshot after = await EntitySnapshotter.CaptureAsync(target);

        EntitySnapshotDiff diff = EntitySnapshotter.Diff(before, after);

        Assert.Equal(-1, diff.CountDeltas[typeof(Order)]);
        Assert.Contains(existing.Id, diff.RemovedKeys[typeof(Order)]);
    }

    [Fact]
    public async Task Diff_NetZero_ButKeyChange_StillFlagsKeyDiff()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget();
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        Order rowA = new() { Name = "RowA" };
        await using (DbContext ctx = target.ResolveContext("orders"))
        {
            ctx.Set<Order>().Add(rowA);
            await ctx.SaveChangesAsync();
        }

        EntitySnapshot before = await EntitySnapshotter.CaptureAsync(target);

        await using (DbContext ctx = target.ResolveContext("orders"))
        {
            ctx.Set<Order>().Remove(rowA);
            ctx.Set<Order>().Add(new Order { Name = "RowB" });
            await ctx.SaveChangesAsync();
        }

        EntitySnapshot after = await EntitySnapshotter.CaptureAsync(target);

        EntitySnapshotDiff diff = EntitySnapshotter.Diff(before, after);

        Assert.Equal(0, diff.CountDeltas[typeof(Order)]);
        Assert.Contains(rowA.Id, diff.RemovedKeys[typeof(Order)]);
        Assert.NotEmpty(diff.AddedKeys[typeof(Order)]);
        Assert.False(diff.IsEmpty);
    }

    [Fact]
    public async Task ToReport_IncludesEntityNamesAndKeys()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget();
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        EntitySnapshot before = await EntitySnapshotter.CaptureAsync(target);

        await using (DbContext ctx = target.ResolveContext("orders"))
        {
            ctx.Set<Order>().Add(new Order { Name = "Reported" });
            await ctx.SaveChangesAsync();
        }

        EntitySnapshot after = await EntitySnapshotter.CaptureAsync(target);

        EntitySnapshotDiff diff = EntitySnapshotter.Diff(before, after);
        string report = diff.ToReport();

        Assert.Contains("Order", report);
        Assert.Contains("+1", report);
    }

    [Fact]
    public async Task CaptureAsync_NullTarget_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await EntitySnapshotter.CaptureAsync(null!));
    }
}

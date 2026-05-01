// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.EFCore;
using Conjecture.Interactions;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.EFCore.Tests;

public class DbTargetExecuteAsyncTests
{
    // ---- test entities ----------------------------------------------------

    private sealed class Order
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class OrderDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

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

    // ---- stub interaction (not a DbInteraction) --------------------------

    private sealed class StubInteraction : IInteraction { }

    private sealed class StubInteractionTarget : IInteractionTarget
    {
        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct) =>
            Task.FromResult<object?>(null);
    }

    // ---- factories -------------------------------------------------------

    private static InMemoryDbTarget CreateInMemoryTarget(string name = "test-db")
    {
        return new(name, () =>
        {
            DbContextOptions<OrderDbContext> opts = new DbContextOptionsBuilder<OrderDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new OrderDbContext(opts);
        });
    }

    private static (SqliteDbTarget target, SqliteConnection connection) CreateSqliteTarget(string name = "test-db")
    {
        SqliteConnection conn = new("Data Source=:memory:");
        conn.Open();
        SqliteDbTarget target = new(name, () =>
        {
            DbContextOptions<OrderDbContext> opts = new DbContextOptionsBuilder<OrderDbContext>()
                .UseSqlite(conn)
                .Options;
            return new OrderDbContext(opts);
        });
        DbContext setupCtx = target.ResolveContext(name);
        setupCtx.Database.EnsureCreated();
        return (target, conn);
    }

    // ---- InMemoryDbTarget tests ------------------------------------------

    [Fact]
    public async Task InMemory_ExecuteAsync_SaveChanges_OnEmptyContext_ReturnsZero()
    {
        InMemoryDbTarget target = CreateInMemoryTarget();
        DbInteraction interaction = new("test-db", DbOpKind.SaveChanges, null);

        object? result = await target.ExecuteAsync(interaction, CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task InMemory_ExecuteAsync_Add_OnFreshContext_ReturnsNull()
    {
        InMemoryDbTarget target = CreateInMemoryTarget();
        Order order = new() { Name = "Widget" };
        DbInteraction interaction = new("test-db", DbOpKind.Add, order);

        object? result = await target.ExecuteAsync(interaction, CancellationToken.None);

        Assert.Null(result);
    }

    // ---- SqliteDbTarget tests --------------------------------------------

    [Fact]
    public async Task Sqlite_ExecuteAsync_Add_OnFreshContext_ReturnsNull()
    {
        (SqliteDbTarget target, SqliteConnection conn) = CreateSqliteTarget();
        await using SqliteConnection _ = conn;
        await using SqliteDbTarget __ = target;
        Order order = new() { Name = "Widget" };
        DbInteraction interaction = new("test-db", DbOpKind.Add, order);

        object? result = await target.ExecuteAsync(interaction, CancellationToken.None);

        Assert.Null(result);
    }

    // ---- validation tests (both targets) ---------------------------------

    [Fact]
    public async Task ExecuteAsync_NullInteraction_Throws_InMemory()
    {
        InMemoryDbTarget target = CreateInMemoryTarget();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => target.ExecuteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_NullInteraction_Throws_Sqlite()
    {
        (SqliteDbTarget target, SqliteConnection conn) = CreateSqliteTarget();
        await using SqliteConnection _ = conn;
        await using SqliteDbTarget __ = target;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => target.ExecuteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_NonDbInteraction_Throws_InMemory()
    {
        InMemoryDbTarget target = CreateInMemoryTarget();
        StubInteraction stub = new();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => target.ExecuteAsync(stub, CancellationToken.None));
        Assert.Contains(nameof(StubInteraction), ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_NonDbInteraction_Throws_Sqlite()
    {
        (SqliteDbTarget target, SqliteConnection conn) = CreateSqliteTarget();
        await using SqliteConnection _ = conn;
        await using SqliteDbTarget __ = target;
        StubInteraction stub = new();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => target.ExecuteAsync(stub, CancellationToken.None));
        Assert.Contains(nameof(StubInteraction), ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_QueryOp_Throws_InMemory()
    {
        InMemoryDbTarget target = CreateInMemoryTarget();
        DbInteraction interaction = new("test-db", DbOpKind.Query, null);

        NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => target.ExecuteAsync(interaction, CancellationToken.None));
        Assert.Contains("Query", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_QueryOp_Throws_Sqlite()
    {
        (SqliteDbTarget target, SqliteConnection conn) = CreateSqliteTarget();
        await using SqliteConnection _ = conn;
        await using SqliteDbTarget __ = target;
        DbInteraction interaction = new("test-db", DbOpKind.Query, null);

        NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => target.ExecuteAsync(interaction, CancellationToken.None));
        Assert.Contains("Query", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sqlite_ExecuteAsync_AfterDispose_Throws()
    {
        (SqliteDbTarget target, SqliteConnection conn) = CreateSqliteTarget();
        await using SqliteConnection _ = conn;
        await target.DisposeAsync();
        DbInteraction interaction = new("test-db", DbOpKind.SaveChanges, null);

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => target.ExecuteAsync(interaction, CancellationToken.None));
    }

    [Fact]
    public async Task Composite_RoutesDbInteraction_SaveChanges_DoesNotThrow()
    {
        (SqliteDbTarget dbTarget, SqliteConnection conn) = CreateSqliteTarget("orders-db");
        await using SqliteConnection _ = conn;
        await using SqliteDbTarget __ = dbTarget;
        StubInteractionTarget stub = new();

        CompositeInteractionTarget composite = new(
            ("other-target", stub),
            ("orders-db", dbTarget));

        DbInteraction interaction = new("orders-db", DbOpKind.SaveChanges, null);

        object? result = await composite.ExecuteAsync(interaction, CancellationToken.None);

        Assert.Equal(0, result);
    }
}
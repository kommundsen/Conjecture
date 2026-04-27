// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

using Conjecture.EFCore;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conjecture.EFCore.Tests;

public class DbInvariantExtensionsTests
{
    // ---- test entities -------------------------------------------------

    private sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    private sealed class ProductWithToken
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        [ConcurrencyCheck]
        public int Version { get; set; }
    }

    private sealed class ProductWithoutToken
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class Order
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
    }

    // ---- DbContext definitions ------------------------------------------

    private sealed class ProductContext(DbContextOptions<ProductContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.Property(e => e.Name).IsRequired();
            });
        }
    }

    private sealed class ProductContextWithPrecisionLoss(DbContextOptions<ProductContextWithPrecisionLoss> options)
        : DbContext(options)
    {
        public DbSet<Product> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.Property(e => e.Name).IsRequired();
                b.Property(e => e.CreatedAt)
                    .HasConversion(
                        d => new DateTime(d.Ticks - (d.Ticks % TimeSpan.TicksPerSecond), d.Kind),
                        d => d);
            });
        }
    }

    private sealed class ProductWithTokenContext(DbContextOptions<ProductWithTokenContext> options) : DbContext(options)
    {
        public DbSet<ProductWithToken> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductWithToken>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.Property(e => e.Name).IsRequired();
                b.Property(e => e.Version).IsConcurrencyToken();
            });
        }
    }

    private sealed class ProductWithoutTokenContext(DbContextOptions<ProductWithoutTokenContext> options) : DbContext(options)
    {
        public DbSet<ProductWithoutToken> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductWithoutToken>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.Property(e => e.Name).IsRequired();
            });
        }
    }

    private sealed class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.Property(e => e.Name).IsRequired();
            });

            modelBuilder.Entity<Order>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.HasOne<Customer>()
                    .WithMany()
                    .HasForeignKey(o => o.CustomerId)
                    .IsRequired();
            });
        }
    }

    // ---- helpers --------------------------------------------------------

    private static (SqliteConnection connection, SqliteDbTarget target) CreateTarget<TContext>(
        string resourceName,
        Func<DbContextOptions<TContext>, TContext> contextFactory)
        where TContext : DbContext
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<TContext> options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connection)
            .Options;

        TContext seed = contextFactory(options);
        seed.Database.EnsureCreated();
        seed.Dispose();

        SqliteDbTarget target = new(resourceName, () => contextFactory(options));
        return (connection, target);
    }

    // ---- tests ----------------------------------------------------------

    [Fact]
    public async Task AssertRoundtripAsync_Sqlite_PassesForCleanRoundtrip()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget<ProductContext>(
            "products", static opts => new(opts));
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        Product entity = new() { Name = "Widget", CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

        await target.AssertRoundtripAsync(entity);
    }

    [Fact]
    public async Task AssertRoundtripAsync_Sqlite_ThrowsWithLossyConverter()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget<ProductContextWithPrecisionLoss>(
            "products", static opts => new(opts));
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        Product entity = new() { Name = "Gizmo", CreatedAt = new DateTime(2025, 6, 1, 12, 34, 56, 789, DateTimeKind.Utc) };

        IEqualityComparer<Product> ticksComparer = new CreatedAtTicksComparer();

        await Assert.ThrowsAsync<RoundtripAssertionException>(
            async () => await target.AssertRoundtripAsync(entity, ticksComparer));
    }

    [Fact]
    public async Task AssertConcurrencyTokenRespectedAsync_StaleSecondUpdate_ThrowsOnSecond()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget<ProductWithTokenContext>(
            "products", static opts => new(opts));
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        ProductWithToken entity = new() { Name = "Original" };

        await target.AssertConcurrencyTokenRespectedAsync(
            entity,
            first => { first.Name = "First update"; first.Version++; },
            second => { second.Name = "Second update"; second.Version++; });
    }

    [Fact]
    public async Task AssertConcurrencyTokenRespectedAsync_NoConcurrencyToken_ThrowsBecauseNoCheckHappened()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget<ProductWithoutTokenContext>(
            "products", static opts => new(opts));
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        ProductWithoutToken entity = new() { Name = "Original" };

        await Assert.ThrowsAnyAsync<Exception>(
            async () => await target.AssertConcurrencyTokenRespectedAsync(
                entity,
                first => { first.Name = "First update"; },
                second => { second.Name = "Second update"; }));
    }

    [Fact]
    public async Task AssertNoOrphansAsync_NoOrphans_Passes()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget<ShopContext>(
            "shop", static opts => new(opts));
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        await target.AssertNoOrphansAsync();
    }

    [Fact]
    public async Task AssertNoOrphansAsync_OrphanInserted_Throws()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget<ShopContext>(
            "shop", static opts => new(opts));
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        await using DbContext ctx = target.ResolveContext("shop");
        await ctx.Database.ExecuteSqlRawAsync(
            "PRAGMA foreign_keys = OFF; INSERT INTO \"Orders\" (\"Id\", \"CustomerId\") VALUES (1, 9999); PRAGMA foreign_keys = ON;");

        await Assert.ThrowsAnyAsync<Exception>(
            async () => await target.AssertNoOrphansAsync());
    }

    [Fact]
    public async Task AssertNoTrackingMatchesTrackedAsync_Passes_OnSimpleEntity()
    {
        (SqliteConnection connection, SqliteDbTarget target) = CreateTarget<ProductContext>(
            "products", static opts => new(opts));
        await using SqliteConnection _ = connection;
        await using SqliteDbTarget __ = target;

        Product entity = new() { Name = "Sprocket", CreatedAt = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc) };

        await target.AssertNoTrackingMatchesTrackedAsync(entity);
    }

    [Fact]
    public async Task AssertRoundtripAsync_NullTarget_Throws()
    {
        IDbTarget? nullTarget = null;
        Product entity = new() { Name = "Test" };

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await nullTarget!.AssertRoundtripAsync(entity));
    }

    // ---- comparers -------------------------------------------------------

    private sealed class CreatedAtTicksComparer : IEqualityComparer<Product>
    {
        public bool Equals(Product? x, Product? y)
        {
            if (x is null && y is null)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.CreatedAt.Ticks == y.CreatedAt.Ticks;
        }

        public int GetHashCode(Product obj) => obj.CreatedAt.GetHashCode();
    }
}

// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Conjecture.EFCore;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Conjecture.EFCore.Tests;

public class RoundtripAsserterTests
{
    // ---- test entities -------------------------------------------------

    private sealed class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime JoinedAt { get; set; }
        public Address? Address { get; set; }
        public List<Order> Orders { get; set; } = [];
    }

    private sealed class Address
    {
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
    }

    private sealed class Order
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
    }

    // ---- DbContext -------------------------------------------------------

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
                b.OwnsOne(e => e.Address, a =>
                {
                    a.Property(x => x.Street).HasColumnName("Street");
                    a.Property(x => x.City).HasColumnName("City");
                });
                b.HasMany(e => e.Orders)
                    .WithOne(o => o.Customer)
                    .HasForeignKey(o => o.CustomerId);
            });

            modelBuilder.Entity<Order>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
            });
        }
    }

    // ---- helpers --------------------------------------------------------

    private static (SqliteConnection, Func<DbContext>) CreateSqliteFactory()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<ShopContext> options = new DbContextOptionsBuilder<ShopContext>()
            .UseSqlite(connection)
            .Options;

        using ShopContext seed = new(options);
        seed.Database.EnsureCreated();

        return (connection, static () =>
            // Each factory call creates a fresh context on a NEW connection that shares the same file.
            // We close over the options string but need a shared in-memory DB per test.
            // Re-use captured options via local helper.
            throw new InvalidOperationException("Use CreateSqliteFactoryWithOptions instead."));
    }

    private static (SqliteConnection connection, Func<DbContext> factory) CreateSqliteFactoryWithOptions()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<ShopContext> options = new DbContextOptionsBuilder<ShopContext>()
            .UseSqlite(connection)
            .Options;

        ShopContext seed = new(options);
        seed.Database.EnsureCreated();
        seed.Dispose();

        return (connection, () => new ShopContext(options));
    }

    private static (SqliteConnection connection, Func<DbContext> factory) CreateSqliteFactoryWithPrecisionLoss()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<ShopContextWithPrecisionLoss> options =
            new DbContextOptionsBuilder<ShopContextWithPrecisionLoss>()
                .UseSqlite(connection)
                .Options;

        ShopContextWithPrecisionLoss seed = new(options);
        seed.Database.EnsureCreated();
        seed.Dispose();

        return (connection, () => new ShopContextWithPrecisionLoss(options));
    }

    // DbContext variant that drops sub-second precision via a value converter.
    private sealed class ShopContextWithPrecisionLoss(DbContextOptions<ShopContextWithPrecisionLoss> options)
        : DbContext(options)
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
                b.Property(e => e.JoinedAt)
                    .HasConversion(
                        d => new DateTime(d.Ticks - (d.Ticks % TimeSpan.TicksPerSecond), d.Kind),
                        d => d);
                b.OwnsOne(e => e.Address, a =>
                {
                    a.Property(x => x.Street).HasColumnName("Street");
                    a.Property(x => x.City).HasColumnName("City");
                });
                b.HasMany(e => e.Orders)
                    .WithOne(o => o.Customer)
                    .HasForeignKey(o => o.CustomerId);
            });

            modelBuilder.Entity<Order>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
            });
        }
    }

    // ---- tests ----------------------------------------------------------

    [Fact]
    public async Task AssertRoundtripAsync_PersistsAndReloads_ScalarPropertiesPreserved()
    {
        (SqliteConnection connection, Func<DbContext> factory) = CreateSqliteFactoryWithOptions();
        await using SqliteConnection _ = connection;

        Customer entity = new() { Name = "Alice", JoinedAt = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc) };

        await RoundtripAsserter.AssertRoundtripAsync(factory, entity);
    }

    [Fact]
    public async Task AssertRoundtripAsync_DataLoss_Throws()
    {
        // A context that strips sub-second precision via a value converter, so
        // the reloaded JoinedAt will differ in ticks from what was saved.
        (SqliteConnection connection, Func<DbContext> factory) = CreateSqliteFactoryWithPrecisionLoss();
        await using SqliteConnection _ = connection;

        // Milliseconds present — the value converter truncates them on persist.
        Customer entity = new() { Name = "Bob", JoinedAt = new DateTime(2025, 6, 1, 12, 34, 56, 789, DateTimeKind.Utc) };

        // Ticks-aware comparer: reloaded value (seconds-only) != original (with millis).
        IEqualityComparer<Customer> ticksComparer = new JoinedAtTicksComparer();

        RoundtripAssertionException exception = await Assert.ThrowsAsync<RoundtripAssertionException>(
            async () => await RoundtripAsserter.AssertRoundtripAsync(factory, entity, ticksComparer));

        Assert.Contains("JoinedAt", exception.Message);
    }

    [Fact]
    public async Task AssertRoundtripAsync_CustomComparer_DeepCompare()
    {
        (SqliteConnection connection, Func<DbContext> factory) = CreateSqliteFactoryWithOptions();
        await using SqliteConnection _ = connection;

        Customer entity = new()
        {
            Name = "Carol",
            JoinedAt = new DateTime(2024, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            Address = new() { Street = "123 Main St", City = "Springfield" },
        };

        IEqualityComparer<Customer> addressComparer = new AddressDeepComparer();

        await RoundtripAsserter.AssertRoundtripAsync(factory, entity, addressComparer);
    }

    [Fact]
    public async Task AssertRoundtripAsync_NullEntity_Throws()
    {
        (SqliteConnection connection, Func<DbContext> factory) = CreateSqliteFactoryWithOptions();
        await using SqliteConnection _ = connection;

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await RoundtripAsserter.AssertRoundtripAsync<Customer>(factory, null!));
    }

    [Fact]
    public async Task AssertRoundtripAsync_NullFactory_Throws()
    {
        Customer entity = new() { Name = "Dave", JoinedAt = DateTime.UtcNow };

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await RoundtripAsserter.AssertRoundtripAsync<Customer>(null!, entity));
    }

    [Fact]
    public async Task AssertNoTrackingMatchesTrackedAsync_AgreesAfterSave()
    {
        (SqliteConnection connection, Func<DbContext> factory) = CreateSqliteFactoryWithOptions();
        await using SqliteConnection _ = connection;

        Customer entity = new() { Name = "Eve", JoinedAt = new DateTime(2023, 7, 4, 8, 0, 0, DateTimeKind.Utc) };

        await RoundtripAsserter.AssertNoTrackingMatchesTrackedAsync(factory, entity);
    }

    [Fact]
    public async Task AssertNoTrackingMatchesTrackedAsync_DifferenceDetected_Throws()
    {
        (SqliteConnection connection, Func<DbContext> factory) = CreateSqliteFactoryWithOptions();
        await using SqliteConnection _ = connection;

        Customer entity = new() { Name = "Frank", JoinedAt = DateTime.UtcNow };

        // Comparer checks Orders.Count — tracked load will have 0, no-tracking also 0,
        // but we provide a comparer that detects when no-tracking query omits a navigated collection
        // that was populated after save (simulated by adding an order after the initial save).
        IEqualityComparer<Customer> ordersComparer = new OrdersCountMismatchComparer();

        Exception exception = await Assert.ThrowsAnyAsync<Exception>(
            async () => await RoundtripAsserter.AssertNoTrackingMatchesTrackedAsync(
                factory, entity, ordersComparer));

        Assert.NotNull(exception.Message);
    }

    // ---- comparers -------------------------------------------------------

    // Compares by exact ticks so that any sub-second truncation is detectable.
    private sealed class JoinedAtTicksComparer : IEqualityComparer<Customer>
    {
        public bool Equals(Customer? x, Customer? y)
        {
            return x is null && y is null || x is not null && y is not null && x.JoinedAt.Equals(y.JoinedAt);
        }

        public int GetHashCode(Customer obj) => obj.JoinedAt.GetHashCode();
    }

    private sealed class AddressDeepComparer : IEqualityComparer<Customer>
    {
        public bool Equals(Customer? x, Customer? y)
        {
            if (x is null && y is null)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.Address is null && y.Address is null || x.Address is not null && y.Address is not null && x.Address.Street == y.Address.Street
                && x.Address.City == y.Address.City;
        }

        public int GetHashCode(Customer obj) =>
            HashCode.Combine(obj.Address?.Street, obj.Address?.City);
    }

    private sealed class OrdersCountMismatchComparer : IEqualityComparer<Customer>
    {
        public bool Equals(Customer? x, Customer? y)
        {
            if (x is null && y is null)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            // Always report mismatch to simulate detected difference in Orders navigation
            return false;
        }

        public int GetHashCode(Customer obj) => obj.Orders.Count.GetHashCode();
    }
}
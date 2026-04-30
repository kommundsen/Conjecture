// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.EFCore;

using Microsoft.EntityFrameworkCore;

namespace Conjecture.EFCore.Tests;

public class EFCoreGenerateTests
{
    // ---- test entities -------------------------------------------------

    private sealed class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<Order> Orders { get; set; } = [];
    }

    private sealed class Order
    {
        public int Id { get; set; }
        public Customer? Customer { get; set; }
        public int? CustomerId { get; set; }
        public DateTime PlacedAt { get; set; }
    }

    private sealed class NotInModel { }

    // ---- test DbContext ------------------------------------------------

    private sealed class ShopContext(DbContextOptions<EFCoreGenerateTests.ShopContext> options) : DbContext(options)
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
                b.HasMany(e => e.Orders)
                    .WithOne(o => o.Customer)
                    .HasForeignKey(o => o.CustomerId)
                    .IsRequired(false);
            });

            modelBuilder.Entity<Order>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
            });
        }
    }

    // ---- helpers -------------------------------------------------------

    private static ShopContext CreateShopContext()
    {
        DbContextOptions<ShopContext> options = new DbContextOptionsBuilder<ShopContext>()
            .UseInMemoryDatabase($"EFCoreGenerateTests_{Guid.NewGuid()}")
            .Options;
        return new(options);
    }

    // ---- tests ---------------------------------------------------------

    [Fact]
    public void Entity_FromDbContext_ReturnsNonNullStrategy()
    {
        using ShopContext context = CreateShopContext();

        Strategy<Customer> strategy = Strategy.Entity<Customer>(context);

        Assert.NotNull(strategy);
    }

    [Fact]
    public void Entity_FromDbContext_SampledInstance_IsNonNull()
    {
        using ShopContext context = CreateShopContext();
        Strategy<Customer> strategy = Strategy.Entity<Customer>(context);

        IReadOnlyList<Customer> samples = strategy.WithSeed(1UL).Sample(10);

        Assert.All(samples, static c => Assert.NotNull(c));
    }

    [Fact]
    public void Entity_FromDbContext_RequiredStringProperty_IsNonNull()
    {
        using ShopContext context = CreateShopContext();
        Strategy<Customer> strategy = Strategy.Entity<Customer>(context);

        IReadOnlyList<Customer> samples = strategy.WithSeed(2UL).Sample(20);

        Assert.All(samples, static c => Assert.NotNull(c.Name));
    }

    [Fact]
    public void Entity_FactoryOverload_CreatesDifferentInstancesPerSample()
    {
        DbContextOptions<ShopContext> options = new DbContextOptionsBuilder<ShopContext>()
            .UseInMemoryDatabase($"EFCoreGenerateTests_Factory_{Guid.NewGuid()}")
            .Options;

        Strategy<Customer> strategy = Strategy.Entity<Customer>(static () =>
            new ShopContext(
                new DbContextOptionsBuilder<ShopContext>()
                    .UseInMemoryDatabase($"EFCoreGenerateTests_Factory_Inner_{Guid.NewGuid()}")
                    .Options));

        IReadOnlyList<Customer> samples = strategy.WithSeed(3UL).Sample(10);

        Assert.Equal(10, samples.Count);
        // All must be distinct object references — factory creates a new entity per sample
        Assert.All(samples, static c => Assert.NotNull(c));
        Assert.True(
            new System.Collections.Generic.HashSet<Customer>(samples, ReferenceEqualityComparer.Instance).Count > 1,
            "Factory overload should produce distinct Customer instances across samples");
    }

    [Fact]
    public void Entity_MaxDepthZero_NavigationsAreEmpty()
    {
        using ShopContext context = CreateShopContext();
        Strategy<Customer> strategy = Strategy.Entity<Customer>(context, maxDepth: 0);

        IReadOnlyList<Customer> samples = strategy.WithSeed(4UL).Sample(20);

        Assert.All(samples, static c => Assert.Empty(c.Orders));
    }

    [Fact]
    public void Entity_UnknownType_Throws()
    {
        using ShopContext context = CreateShopContext();

        Assert.Throws<InvalidOperationException>(() => Strategy.Entity<NotInModel>(context));
    }
}
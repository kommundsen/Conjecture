// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Conjecture.Core;
using Conjecture.EFCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.EFCore.Tests;

public class EntityStrategyBuilderTests
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

    private sealed class Product
    {
        public int Id { get; set; }
        public Address ShippingAddress { get; set; } = new();
    }

    private sealed class Address
    {
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
    }

    private sealed class NotInModelType { }

    // ---- test DbContext ------------------------------------------------

    private sealed class ShopContext(DbContextOptions<EntityStrategyBuilderTests.ShopContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;

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

            modelBuilder.Entity<Product>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.OwnsOne(e => e.ShippingAddress);
            });
        }
    }

    // ---- helpers -------------------------------------------------------

    private static ShopContext CreateShopContext()
    {
        DbContextOptions<ShopContext> options = new DbContextOptionsBuilder<ShopContext>()
            .UseInMemoryDatabase($"EntityStrategyBuilderTests_{Guid.NewGuid()}")
            .Options;
        return new(options);
    }

    // ---- tests ---------------------------------------------------------

    [Fact]
    public void Build_ScalarProperties_ProduceValuesViaPropertyStrategyBuilder()
    {
        using ShopContext context = CreateShopContext();
        IModel model = context.Model;
        EntityStrategyBuilder builder = new(model);

        Strategy<Customer> strategy = builder.Build<Customer>();
        IReadOnlyList<Customer> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, static c =>
        {
            Assert.NotNull(c.Name);
            // Id is ValueGeneratedOnAdd — left at CLR default (0)
            Assert.Equal(0, c.Id);
        });
    }

    [Fact]
    public void Build_RequiredOwnedNavigation_GeneratedInline()
    {
        using ShopContext context = CreateShopContext();
        IModel model = context.Model;
        EntityStrategyBuilder builder = new(model);

        Strategy<Product> strategy = builder.Build<Product>();
        IReadOnlyList<Product> samples = DataGen.Sample(strategy, count: 20, seed: 2UL);

        Assert.All(samples, static p =>
        {
            Assert.NotNull(p.ShippingAddress);
            Assert.NotNull(p.ShippingAddress.Street);
            Assert.NotNull(p.ShippingAddress.City);
        });
    }

    [Fact]
    public void Build_OptionalNavigation_AtDepthBound_IsNull()
    {
        using ShopContext context = CreateShopContext();
        IModel model = context.Model;
        EntityStrategyBuilder builder = new EntityStrategyBuilder(model).WithMaxDepth(0);

        Strategy<Order> strategy = builder.Build<Order>();
        IReadOnlyList<Order> samples = DataGen.Sample(strategy, count: 20, seed: 3UL);

        Assert.All(samples, static o => Assert.Null(o.Customer));
    }

    [Fact]
    public void Build_DefaultMaxDepth_IsTwo()
    {
        using ShopContext context = CreateShopContext();
        IModel model = context.Model;
        EntityStrategyBuilder builder = new(model);

        Strategy<Customer> strategy = builder.Build<Customer>();
        // Take several samples for robustness; at depth 2, Customer.Orders[*].Customer must be null
        IReadOnlyList<Customer> samples = DataGen.Sample(strategy, count: 30, seed: 4UL);

        // At least some orders should be present across samples
        bool anyOrders = samples.Any(static c => c.Orders.Count > 0);
        Assert.True(anyOrders, "Default depth 2 should populate Orders at depth 1");

        // At depth 2, the Order's Customer back-reference must be null (cycle terminated)
        Assert.All(samples, static c => Assert.All(c.Orders, static o => Assert.Null(o.Customer)));
    }

    [Fact]
    public void Build_WithoutNavigation_OmitsNavigation()
    {
        using ShopContext context = CreateShopContext();
        IModel model = context.Model;
        EntityStrategyBuilder builder = new EntityStrategyBuilder(model)
            .WithoutNavigation<Customer>(static c => c.Orders);

        Strategy<Customer> strategy = builder.Build<Customer>();
        IReadOnlyList<Customer> samples = DataGen.Sample(strategy, count: 20, seed: 5UL);

        Assert.All(samples, static c => Assert.Empty(c.Orders));
    }

    [Fact]
    public void Build_UnknownEntityType_Throws()
    {
        using ShopContext context = CreateShopContext();
        IModel model = context.Model;
        EntityStrategyBuilder builder = new(model);

        Assert.Throws<InvalidOperationException>(static () =>
        {
            EntityStrategyBuilder localBuilder = new(new ShopContext(
                new DbContextOptionsBuilder<ShopContext>()
                    .UseInMemoryDatabase("throw_test")
                    .Options)
                .Model);
            localBuilder.Build<NotInModelType>();
        });
    }
}
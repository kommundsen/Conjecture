// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq;

using Conjecture.Core;
using Conjecture.EFCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.EFCore.Tests;

public class DbStrategiesTests
{
    // ---- test entity ---------------------------------------------------

    private sealed class Order
    {
        public int Id { get; set; }
        public string Description { get; set; } = "";
    }

    // ---- test DbContext ------------------------------------------------

    private sealed class OrderDbContext(DbContextOptions<DbStrategiesTests.OrderDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.Property(e => e.Description).IsRequired();
            });
        }
    }

    // ---- helpers -------------------------------------------------------

    private static IModel CreateOrderModel()
    {
        DbContextOptions<OrderDbContext> options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase($"DbStrategiesTests_{Guid.NewGuid()}")
            .Options;
        using OrderDbContext context = new(options);
        return context.Model;
    }

    // ---- tests ---------------------------------------------------------

    [Fact]
    public void Generate_Db_Add_ProducesAddInteraction()
    {
        IModel model = CreateOrderModel();
        Strategy<DbInteraction> strategy = Strategy.Db.Add<Order>("orders-db", model);

        IReadOnlyList<DbInteraction> samples = strategy.WithSeed(1UL).Sample(20);

        Assert.All(samples, static s =>
        {
            Assert.Equal(DbOpKind.Add, s.Op);
            Assert.Equal("orders-db", s.ResourceName);
            Assert.True(s.Payload is Order, $"Payload should be Order but was {s.Payload?.GetType().Name}");
        });
    }

    [Fact]
    public void Generate_Db_Update_ProducesUpdateInteraction()
    {
        IModel model = CreateOrderModel();
        Strategy<DbInteraction> strategy = Strategy.Db.Update<Order>("orders-db", model);

        IReadOnlyList<DbInteraction> samples = strategy.WithSeed(2UL).Sample(20);

        Assert.All(samples, static s =>
        {
            Assert.Equal(DbOpKind.Update, s.Op);
            Assert.Equal("orders-db", s.ResourceName);
            Assert.True(s.Payload is Order, $"Payload should be Order but was {s.Payload?.GetType().Name}");
        });
    }

    [Fact]
    public void Generate_Db_Remove_ProducesRemoveInteraction()
    {
        IModel model = CreateOrderModel();
        Strategy<DbInteraction> strategy = Strategy.Db.Remove<Order>("orders-db", model);

        IReadOnlyList<DbInteraction> samples = strategy.WithSeed(3UL).Sample(20);

        Assert.All(samples, static s =>
        {
            Assert.Equal(DbOpKind.Remove, s.Op);
            Assert.Equal("orders-db", s.ResourceName);
            Assert.True(s.Payload is Order, $"Payload should be Order but was {s.Payload?.GetType().Name}");
        });
    }

    [Fact]
    public void Generate_Db_SaveChanges_ProducesSaveChangesInteraction()
    {
        Strategy<DbInteraction> strategy = Strategy.Db.SaveChanges("orders-db");

        IReadOnlyList<DbInteraction> samples = strategy.WithSeed(4UL).Sample(10);

        Assert.All(samples, static s =>
        {
            Assert.Equal(DbOpKind.SaveChanges, s.Op);
            Assert.Equal("orders-db", s.ResourceName);
            Assert.Null(s.Payload);
        });
    }

    [Fact]
    public void Generate_Db_Sequence_ProducesListInRange()
    {
        IModel model = CreateOrderModel();
        Strategy<IReadOnlyList<DbInteraction>> strategy = Strategy.Db.Sequence("orders-db", model).WithLength(min: 2, max: 4).Build();

        IReadOnlyList<IReadOnlyList<DbInteraction>> samples = strategy.WithSeed(5UL).Sample(30);

        Assert.All(samples, static list => Assert.True(list.Count >= 2 && list.Count <= 4,
                $"Expected Count in [2,4] but got {list.Count}"));
    }

    [Fact]
    public void Generate_Db_Sequence_ListContainsValidInteractions()
    {
        IModel model = CreateOrderModel();
        Strategy<IReadOnlyList<DbInteraction>> strategy = Strategy.Db.Sequence("orders-db", model).WithLength(min: 1, max: 5).Build();

        IReadOnlyList<IReadOnlyList<DbInteraction>> samples = strategy.WithSeed(6UL).Sample(30);

        Assert.All(samples, static list => Assert.All(list, static interaction =>
            {
                Assert.NotNull(interaction);
                Assert.Equal("orders-db", interaction.ResourceName);
            }));
    }

    [Fact]
    public void Generate_Db_Add_NullModel_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Strategy.Db.Add<Order>("orders-db", null!));
    }

    [Fact]
    public void Generate_Db_Add_NullResourceName_Throws()
    {
        IModel model = CreateOrderModel();

        Assert.Throws<ArgumentNullException>(() => Strategy.Db.Add<Order>(null!, model));
    }
}
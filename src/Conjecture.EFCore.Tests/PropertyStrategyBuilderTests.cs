// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Conjecture.Core;
using Conjecture.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.EFCore.Tests;

public class PropertyStrategyBuilderTests
{
    // ---- test entities -------------------------------------------------

    private enum Status { Active, Inactive, Pending }

    private sealed class AllTypesEntity
    {
        public int Id { get; set; }
        public int? NullableInt { get; set; }
        [MaxLength(10)]
        public string? StringWithMaxLength { get; set; }
        public string RequiredString { get; set; } = "";
        public decimal DecimalValue { get; set; }
        public Guid GuidValue { get; set; }
        public Status StatusValue { get; set; }
    }

    private sealed class ValueGeneratedEntity
    {
        public int Id { get; set; }
    }

    private sealed class AllTypesContext : DbContext
    {
        public AllTypesContext(DbContextOptions<AllTypesContext> options) : base(options) { }

        public DbSet<AllTypesEntity> AllTypes { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AllTypesEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.RequiredString).IsRequired();
                b.Property(e => e.DecimalValue).HasPrecision(8, 2);
            });
        }
    }

    private sealed class ValueGeneratedContext : DbContext
    {
        public ValueGeneratedContext(DbContextOptions<ValueGeneratedContext> options) : base(options) { }

        public DbSet<ValueGeneratedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ValueGeneratedEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
            });
        }
    }

    // ---- helpers -------------------------------------------------------

    private static IProperty GetProperty<TEntity>(DbContext context, string propertyName)
        => context.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!;

    private static AllTypesContext CreateAllTypesContext()
    {
        DbContextOptions<AllTypesContext> options = new DbContextOptionsBuilder<AllTypesContext>()
            .UseInMemoryDatabase("PropertyStrategyBuilderTests_AllTypes")
            .Options;
        return new(options);
    }

    private static ValueGeneratedContext CreateValueGeneratedContext()
    {
        DbContextOptions<ValueGeneratedContext> options = new DbContextOptionsBuilder<ValueGeneratedContext>()
            .UseInMemoryDatabase("PropertyStrategyBuilderTests_ValueGenerated")
            .Options;
        return new(options);
    }

    // ---- tests ---------------------------------------------------------

    [Fact]
    public void Build_NonNullableInt_ProducesNonNullInt32()
    {
        using AllTypesContext context = CreateAllTypesContext();
        IProperty property = GetProperty<AllTypesEntity>(context, nameof(AllTypesEntity.Id));

        Strategy<object?> strategy = PropertyStrategyBuilder.Build(property);
        System.Collections.Generic.IReadOnlyList<object?> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, static s =>
        {
            Assert.NotNull(s);
            Assert.IsType<int>(s);
        });
    }

    [Fact]
    public void Build_NullableInt_AllowsNull()
    {
        using AllTypesContext context = CreateAllTypesContext();
        IProperty property = GetProperty<AllTypesEntity>(context, nameof(AllTypesEntity.NullableInt));

        Strategy<object?> strategy = PropertyStrategyBuilder.Build(property);
        System.Collections.Generic.IReadOnlyList<object?> samples = DataGen.Sample(strategy, count: 100, seed: 2UL);

        bool hasNull = samples.Any(static s => s is null);
        bool hasInt = samples.Any(static s => s is int);
        Assert.True(hasNull, "Nullable int property strategy must emit null values");
        Assert.True(hasInt, "Nullable int property strategy must emit int values");
    }

    [Fact]
    public void Build_StringMaxLength_RespectsBound()
    {
        using AllTypesContext context = CreateAllTypesContext();
        IProperty property = GetProperty<AllTypesEntity>(context, nameof(AllTypesEntity.StringWithMaxLength));

        Strategy<object?> strategy = PropertyStrategyBuilder.Build(property);
        System.Collections.Generic.IReadOnlyList<object?> samples = DataGen.Sample(strategy, count: 50, seed: 3UL);

        Assert.All(samples, static s =>
        {
            if (s is string str)
            {
                Assert.True(str.Length <= 10, $"String length {str.Length} exceeds MaxLength(10)");
            }
        });
    }

    [Fact]
    public void Build_DecimalPrecisionScale_RespectsScale()
    {
        using AllTypesContext context = CreateAllTypesContext();
        IProperty property = GetProperty<AllTypesEntity>(context, nameof(AllTypesEntity.DecimalValue));

        Strategy<object?> strategy = PropertyStrategyBuilder.Build(property);
        System.Collections.Generic.IReadOnlyList<object?> samples = DataGen.Sample(strategy, count: 50, seed: 4UL);

        Assert.All(samples, static s =>
        {
            Assert.NotNull(s);
            decimal value = Assert.IsType<decimal>(s);
            // magnitude within precision(8,2): integer part <= 10^6
            Assert.True(System.Math.Abs(value) <= 1_000_000m, $"Value {value} exceeds magnitude bound");
            // at most 2 fractional digits
            decimal fractionalPart = System.Math.Abs(value - System.Math.Truncate(value));
            Assert.True(fractionalPart * 100 == System.Math.Truncate(fractionalPart * 100),
                $"Value {value} has more than 2 fractional digits");
        });
    }

    [Fact]
    public void Build_ValueGeneratedOnAdd_ReturnsClrDefaultStrategy()
    {
        using ValueGeneratedContext context = CreateValueGeneratedContext();
        IProperty property = GetProperty<ValueGeneratedEntity>(context, nameof(ValueGeneratedEntity.Id));

        Strategy<object?> strategy = PropertyStrategyBuilder.Build(property);
        System.Collections.Generic.IReadOnlyList<object?> samples = DataGen.Sample(strategy, count: 10, seed: 5UL);

        Assert.All(samples, static s => Assert.Equal(0, s));
    }

    [Fact]
    public void Build_GuidProperty_ProducesGuid()
    {
        using AllTypesContext context = CreateAllTypesContext();
        IProperty property = GetProperty<AllTypesEntity>(context, nameof(AllTypesEntity.GuidValue));

        Strategy<object?> strategy = PropertyStrategyBuilder.Build(property);
        System.Collections.Generic.IReadOnlyList<object?> samples = DataGen.Sample(strategy, count: 20, seed: 6UL);

        Assert.All(samples, static s =>
        {
            Assert.NotNull(s);
            Guid guid = Assert.IsType<Guid>(s);
            Assert.NotEqual(Guid.Empty, guid);
        });
    }

    [Fact]
    public void Build_StringNonNullableNoMaxLength_ProducesNonNullString()
    {
        using AllTypesContext context = CreateAllTypesContext();
        IProperty property = GetProperty<AllTypesEntity>(context, nameof(AllTypesEntity.RequiredString));

        Strategy<object?> strategy = PropertyStrategyBuilder.Build(property);
        System.Collections.Generic.IReadOnlyList<object?> samples = DataGen.Sample(strategy, count: 20, seed: 7UL);

        Assert.All(samples, static s =>
        {
            Assert.NotNull(s);
            Assert.IsType<string>(s);
        });
    }
}

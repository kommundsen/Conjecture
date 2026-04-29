// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading.Tasks;

using Conjecture.EFCore;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Conjecture.EFCore.Tests;

public sealed class MigrationHarnessTests
{
    // ---- symmetric migration fixtures ------------------------------------

    [DbContext(typeof(SymmetricMigrationContext))]
    [Migration("20240101000000_Initial")]
    private sealed class SymmetricInitialMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                "Items",
                columns: static t => new
                {
                    Id = t.Column<int>(nullable: false),
                    Name = t.Column<string>(nullable: false),
                },
                constraints: static t => t.PrimaryKey("PK_Items", static x => x.Id));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Items");
        }
    }

    [DbContext(typeof(SymmetricMigrationContext))]
    [Migration("20240102000000_AddPrices")]
    private sealed class SymmetricAddPriceMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                "Prices",
                columns: static t => new
                {
                    Id = t.Column<int>(nullable: false),
                    Amount = t.Column<decimal>(nullable: false),
                },
                constraints: static t => t.PrimaryKey("PK_Prices", static x => x.Id));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Prices");
        }
    }

    private sealed class SymmetricMigrationContextSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "9.0.0");
            modelBuilder.Entity(
                "Item",
                static b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd();
                    b.Property<string>("Name").IsRequired();
                    b.HasKey("Id");
                    b.ToTable("Items");
                });
            modelBuilder.Entity(
                "Price",
                static b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd();
                    b.Property<decimal>("Amount");
                    b.HasKey("Id");
                    b.ToTable("Prices");
                });
        }
    }

    private sealed class SymmetricMigrationContext(DbContextOptions<MigrationHarnessTests.SymmetricMigrationContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(
                "Item",
                static (EntityTypeBuilder b) =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd();
                    b.Property<string>("Name").IsRequired();
                    b.HasKey("Id");
                    b.ToTable("Items");
                });
            modelBuilder.Entity(
                "Price",
                static (EntityTypeBuilder b) =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd();
                    b.Property<decimal>("Amount");
                    b.HasKey("Id");
                    b.ToTable("Prices");
                });
        }
    }

    // ---- asymmetric migration fixtures -----------------------------------

    [DbContext(typeof(AsymmetricMigrationContext))]
    [Migration("20240101000000_Initial")]
    private sealed class AsymmetricInitialMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                "Products",
                columns: static t => new
                {
                    Id = t.Column<int>(nullable: false),
                    Title = t.Column<string>(nullable: false),
                },
                constraints: static t => t.PrimaryKey("PK_Products", static x => x.Id));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Products");
        }
    }

    [DbContext(typeof(AsymmetricMigrationContext))]
    [Migration("20240102000000_AddRatings")]
    private sealed class AsymmetricAddRatingMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                "Ratings",
                columns: static t => new
                {
                    Id = t.Column<int>(nullable: false),
                    Score = t.Column<int>(nullable: false),
                },
                constraints: static t => t.PrimaryKey("PK_Ratings", static x => x.Id));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally wrong: drops Products instead of Ratings,
            // leaving Ratings behind — schema after Down != schema before Up.
            migrationBuilder.DropTable("Products");
        }
    }

    private sealed class AsymmetricMigrationContextSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "9.0.0");
            modelBuilder.Entity(
                "Product",
                static b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd();
                    b.Property<string>("Title").IsRequired();
                    b.HasKey("Id");
                    b.ToTable("Products");
                });
            modelBuilder.Entity(
                "Rating",
                static b =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd();
                    b.Property<int>("Score");
                    b.HasKey("Id");
                    b.ToTable("Ratings");
                });
        }
    }

    private sealed class AsymmetricMigrationContext(DbContextOptions<MigrationHarnessTests.AsymmetricMigrationContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(
                "Product",
                static (EntityTypeBuilder b) =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd();
                    b.Property<string>("Title").IsRequired();
                    b.HasKey("Id");
                    b.ToTable("Products");
                });
            modelBuilder.Entity(
                "Rating",
                static (EntityTypeBuilder b) =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd();
                    b.Property<int>("Score");
                    b.HasKey("Id");
                    b.ToTable("Ratings");
                });
        }
    }

    // ---- no-migrations fixture -------------------------------------------

    private sealed class NoMigrationsContext(DbContextOptions<MigrationHarnessTests.NoMigrationsContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity(
                "Widget",
                static (EntityTypeBuilder b) =>
                {
                    b.Property<int>("Id").ValueGeneratedOnAdd();
                    b.Property<string>("Label").IsRequired();
                    b.HasKey("Id");
                    b.ToTable("Widgets");
                });
        }
    }

    // ---- helpers ---------------------------------------------------------

    private static DbContextOptions<T> SqliteOptions<T>(string dbName)
        where T : DbContext
    {
        string connectionString = FormattableString.Invariant(
            $"Data Source=file:{dbName}?mode=memory&cache=shared");

        return new DbContextOptionsBuilder<T>()
            .UseSqlite(connectionString)
            .Options;
    }

    // ---- tests -----------------------------------------------------------

    [Fact]
    public async Task AssertUpDownIdempotentAsync_SymmetricMigrations_DoesNotThrow()
    {
        DbContextOptions<SymmetricMigrationContext> options =
            SqliteOptions<SymmetricMigrationContext>("symmetric_harness");

        await using SymmetricMigrationContext context = new(options);

        await MigrationHarness.AssertUpDownIdempotentAsync(context);
    }

    [Fact]
    public async Task AssertUpDownIdempotentAsync_AsymmetricMigrations_Throws()
    {
        DbContextOptions<AsymmetricMigrationContext> options =
            SqliteOptions<AsymmetricMigrationContext>("asymmetric_harness");

        await using AsymmetricMigrationContext context = new(options);

        await Assert.ThrowsAsync<MigrationAssertionException>(
            async () => await MigrationHarness.AssertUpDownIdempotentAsync(context));
    }

    [Fact]
    public async Task AssertUpDownIdempotentAsync_NoMigrations_Throws()
    {
        DbContextOptions<NoMigrationsContext> options =
            SqliteOptions<NoMigrationsContext>("no_migrations_harness");

        await using NoMigrationsContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        Exception exception = await Assert.ThrowsAnyAsync<Exception>(
            async () => await MigrationHarness.AssertUpDownIdempotentAsync(context));

        Assert.Contains("migration", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssertUpDownIdempotentAsync_NullContext_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await MigrationHarness.AssertUpDownIdempotentAsync(null!));
    }
}
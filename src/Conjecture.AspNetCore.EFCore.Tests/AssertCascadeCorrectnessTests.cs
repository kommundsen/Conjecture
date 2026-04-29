// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.AspNetCore.EFCore;
using Conjecture.EFCore;
using Conjecture.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore.EFCore.Tests;

/// <summary>
/// Per-test fixture: each instance gets a uniquely-named shared-cache SQLite DB so tests are fully isolated.
/// </summary>
internal sealed class CascadeTestFixture : IDisposable
{
    private readonly SqliteConnection keepalive;

    public string ConnectionString { get; }
    public WebApplicationFactory<TestApp> Factory { get; }

    public CascadeTestFixture()
    {
        string dbName = $"cascade-{Guid.NewGuid():N}";
        ConnectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";

        // Keep at least one connection open so the shared-cache DB isn't flushed.
        keepalive = new(ConnectionString);
        keepalive.Open();

        Factory = new WebApplicationFactory<TestApp>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddDbContext<CascadeDbContext>(opts =>
                    opts.UseSqlite(ConnectionString));

                // Expose the connection string so buggy endpoints can open a second FK=OFF connection.
                services.AddSingleton(ConnectionString);
            });

            builder.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapDelete("/customers/{id:int}", async context =>
                    {
                        int id = int.Parse((string)context.Request.RouteValues["id"]!);
                        CascadeDbContext db = context.RequestServices.GetRequiredService<CascadeDbContext>();
                        CascadeCustomer? customer = await db.Customers.FindAsync(id);
                        if (customer is null)
                        {
                            context.Response.StatusCode = 404;
                            return;
                        }

                        db.Customers.Remove(customer);
                        await db.SaveChangesAsync();
                        context.Response.StatusCode = 204;
                    });

                    endpoints.MapDelete("/customers/{id:int}/buggy-cascade", async context =>
                    {
                        int id = int.Parse((string)context.Request.RouteValues["id"]!);
                        string connStr = context.RequestServices.GetRequiredService<string>();

                        // Open a SECOND connection with FK=OFF so cascade does NOT fire — creating orphan Orders.
                        using SqliteConnection fkOff = new(connStr);
                        await fkOff.OpenAsync();
                        SqliteCommand pragma = fkOff.CreateCommand();
                        pragma.CommandText = "PRAGMA foreign_keys=OFF;";
                        await pragma.ExecuteNonQueryAsync();

                        SqliteCommand delete = fkOff.CreateCommand();
                        delete.CommandText = "DELETE FROM Customers WHERE Id = $id;";
                        delete.Parameters.AddWithValue("$id", id);
                        await delete.ExecuteNonQueryAsync();

                        context.Response.StatusCode = 200;
                    });

                    endpoints.MapDelete("/customers/{id:int}/buggy-setnull", async context =>
                    {
                        int id = int.Parse((string)context.Request.RouteValues["id"]!);
                        string connStr = context.RequestServices.GetRequiredService<string>();

                        // Open a SECOND connection with FK=OFF; delete Customer without nulling Profile.OwnerId.
                        using SqliteConnection fkOff = new(connStr);
                        await fkOff.OpenAsync();
                        SqliteCommand pragma = fkOff.CreateCommand();
                        pragma.CommandText = "PRAGMA foreign_keys=OFF;";
                        await pragma.ExecuteNonQueryAsync();

                        SqliteCommand delete = fkOff.CreateCommand();
                        delete.CommandText = "DELETE FROM Customers WHERE Id = $id;";
                        delete.Parameters.AddWithValue("$id", id);
                        await delete.ExecuteNonQueryAsync();

                        context.Response.StatusCode = 200;
                    });

                    endpoints.MapDelete("/customers/restrict-fail", context =>
                    {
                        context.Response.StatusCode = 400;
                        return context.Response.WriteAsync("Cannot delete: restrict");
                    });
                });
            });
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        CascadeDbContext ctx = scope.ServiceProvider.GetRequiredService<CascadeDbContext>();
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Factory.Dispose();
        keepalive.Dispose();
    }

    internal sealed class CascadeCustomer
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    internal sealed class CascadeOrder
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public CascadeCustomer? Customer { get; set; }
    }

    internal sealed class CascadeProfile
    {
        public int Id { get; set; }
        public int? OwnerId { get; set; }
        public CascadeCustomer? Owner { get; set; }
    }

    internal sealed class CascadeDbContext(DbContextOptions<CascadeDbContext> opts) : DbContext(opts)
    {
        public DbSet<CascadeCustomer> Customers => Set<CascadeCustomer>();
        public DbSet<CascadeOrder> Orders => Set<CascadeOrder>();
        public DbSet<CascadeProfile> Profiles => Set<CascadeProfile>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CascadeOrder>()
                .HasOne(static o => o.Customer)
                .WithMany()
                .HasForeignKey(static o => o.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CascadeProfile>()
                .HasOne(static p => p.Owner)
                .WithMany()
                .HasForeignKey(static p => p.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

public sealed class AssertCascadeCorrectnessTests : IDisposable
{
    private readonly CascadeTestFixture fixture;

    public AssertCascadeCorrectnessTests()
    {
        fixture = new();
    }

    public void Dispose()
    {
        fixture.Dispose();
    }

    private IHost Host => fixture.Factory.Services.GetRequiredService<IHost>();

    private AspNetCoreEFCoreInvariants CreateInvariants()
    {
        HttpClient client = fixture.Factory.CreateClient();
        HostHttpTarget http = new(Host, client);
        AspNetCoreDbTarget<CascadeTestFixture.CascadeDbContext> db = new(Host, "cascade-db");
        return new AspNetCoreEFCoreInvariants(http, db);
    }

    private int SeedCustomerWithOrder()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        CascadeTestFixture.CascadeDbContext ctx = scope.ServiceProvider.GetRequiredService<CascadeTestFixture.CascadeDbContext>();
        CascadeTestFixture.CascadeCustomer customer = new() { Name = "Alice" };
        ctx.Customers.Add(customer);
        ctx.SaveChanges();
        CascadeTestFixture.CascadeOrder order = new() { CustomerId = customer.Id };
        ctx.Orders.Add(order);
        ctx.SaveChanges();
        return customer.Id;
    }

    private int SeedCustomerWithProfile()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        CascadeTestFixture.CascadeDbContext ctx = scope.ServiceProvider.GetRequiredService<CascadeTestFixture.CascadeDbContext>();
        CascadeTestFixture.CascadeCustomer customer = new() { Name = "Bob" };
        ctx.Customers.Add(customer);
        ctx.SaveChanges();
        CascadeTestFixture.CascadeProfile profile = new() { OwnerId = customer.Id };
        ctx.Profiles.Add(profile);
        ctx.SaveChanges();
        return customer.Id;
    }

    [Fact]
    public async Task Cascade_RootDeleted_DependentsAlsoRemoved_Passes()
    {
        int customerId = SeedCustomerWithOrder();
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        await invariants.AssertCascadeCorrectnessAsync(
            async (client, ct) =>
                await client.DeleteAsync(
                    FormattableString.Invariant($"/customers/{customerId}"), ct),
            typeof(CascadeTestFixture.CascadeCustomer));
    }

    [Fact]
    public async Task Cascade_DependentsSurviveBug_Throws()
    {
        int customerId = SeedCustomerWithOrder();
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        AspNetCoreEFCoreInvariantException ex = await Assert.ThrowsAsync<AspNetCoreEFCoreInvariantException>(
            () => invariants.AssertCascadeCorrectnessAsync(
                async (client, ct) =>
                    await client.DeleteAsync(
                        FormattableString.Invariant($"/customers/{customerId}/buggy-cascade"), ct),
                typeof(CascadeTestFixture.CascadeCustomer)));

        Assert.Contains("Customer", ex.Message);
        Assert.Contains("Order", ex.Message);
        Assert.Contains("Cascade", ex.Message);
    }

    [Fact]
    public async Task SetNull_DependentFkBecomesNull_Passes()
    {
        int customerId = SeedCustomerWithProfile();
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        await invariants.AssertCascadeCorrectnessAsync(
            async (client, ct) =>
                await client.DeleteAsync(
                    FormattableString.Invariant($"/customers/{customerId}"), ct),
            typeof(CascadeTestFixture.CascadeCustomer));
    }

    [Fact]
    public async Task SetNull_DependentStillReferencesDeletedKey_Throws()
    {
        int customerId = SeedCustomerWithProfile();
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        AspNetCoreEFCoreInvariantException ex = await Assert.ThrowsAsync<AspNetCoreEFCoreInvariantException>(
            () => invariants.AssertCascadeCorrectnessAsync(
                async (client, ct) =>
                    await client.DeleteAsync(
                        FormattableString.Invariant($"/customers/{customerId}/buggy-setnull"), ct),
                typeof(CascadeTestFixture.CascadeCustomer)));

        Assert.Contains("SetNull", ex.Message);
    }

    [Fact]
    public async Task Restrict_DeleteFailed_NoChange_ReturnsEarly()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        await invariants.AssertCascadeCorrectnessAsync(
            static async (client, ct) =>
                await client.DeleteAsync("/customers/restrict-fail", ct),
            typeof(CascadeTestFixture.CascadeCustomer));
    }

    [Fact]
    public async Task NullDeleteRequest_Throws()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => invariants.AssertCascadeCorrectnessAsync(
                null!,
                typeof(CascadeTestFixture.CascadeCustomer)));
    }

    [Fact]
    public async Task NullRootEntityType_Throws()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => invariants.AssertCascadeCorrectnessAsync(
                static async (client, ct) =>
                    await client.DeleteAsync("/customers/1", ct),
                null!));
    }
}
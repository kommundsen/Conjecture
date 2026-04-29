// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Net;
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

public sealed class PartialWritesFixture : IDisposable
{
    private readonly SqliteConnection connection;

    public WebApplicationFactory<TestApp> Factory { get; }

    public PartialWritesFixture()
    {
        connection = new("DataSource=:memory:");
        connection.Open();

        Factory = new WebApplicationFactory<TestApp>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddDbContext<OrdersDb>(opts =>
                    opts.UseSqlite(connection)));

            builder.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapPost("/orders/happy", async context =>
                    {
                        OrdersDb db = context.RequestServices.GetRequiredService<OrdersDb>();
                        db.Orders.Add(new OrderEntity { Name = "happy" });
                        await db.SaveChangesAsync();
                        await context.Response.WriteAsync("ok");
                    });

                    endpoints.MapPost("/orders/rigged-failure", async context =>
                    {
                        OrdersDb db = context.RequestServices.GetRequiredService<OrdersDb>();
                        db.Orders.Add(new OrderEntity { Name = "rigged" });
                        await db.SaveChangesAsync();
                        context.Response.StatusCode = 503;
                        await context.Response.WriteAsync("error");
                    });

                    endpoints.MapPost("/orders/clean-failure", async context =>
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync("bad request");
                    });

                    endpoints.MapPost("/orders/clean-500", async context =>
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("server error");
                    });

                    endpoints.MapPost("/orders/rigged-400", async context =>
                    {
                        OrdersDb db = context.RequestServices.GetRequiredService<OrdersDb>();
                        db.Orders.Add(new OrderEntity { Name = "rigged-400" });
                        await db.SaveChangesAsync();
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync("bad request but wrote");
                    });
                });
            });
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        OrdersDb ctx = scope.ServiceProvider.GetRequiredService<OrdersDb>();
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Factory.Dispose();
        connection.Dispose();
    }

    internal sealed class OrdersDb(DbContextOptions<OrdersDb> opts) : DbContext(opts)
    {
        public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    }

    internal sealed class OrderEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}

public sealed class AssertNoPartialWritesTests(PartialWritesFixture fixture) : IClassFixture<PartialWritesFixture>
{
    private readonly PartialWritesFixture fixture = fixture;

    private IHost Host => fixture.Factory.Services.GetRequiredService<IHost>();

    private AspNetCoreEFCoreInvariants CreateInvariants()
    {
        HttpClient client = fixture.Factory.CreateClient();
        HostHttpTarget http = new(Host, client);
        AspNetCoreDbTarget<PartialWritesFixture.OrdersDb> db = new(Host, "orders-db");
        return new AspNetCoreEFCoreInvariants(http, db);
    }

    [Fact]
    public async Task Returns200_WritesAllowed_DoesNotThrow()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        await invariants.AssertNoPartialWritesOnErrorAsync(
            static async (client, ct) =>
                await client.PostAsync("/orders/happy", null, ct));
    }

    [Fact]
    public async Task Returns400_NoWrites_DoesNotThrow()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        await invariants.AssertNoPartialWritesOnErrorAsync(
            static async (client, ct) =>
                await client.PostAsync("/orders/clean-failure", null, ct));
    }

    [Fact]
    public async Task Returns500_NoWrites_DoesNotThrow()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        await invariants.AssertNoPartialWritesOnErrorAsync(
            static async (client, ct) =>
                await client.PostAsync("/orders/clean-500", null, ct));
    }

    [Fact]
    public async Task Returns503_WithPartialWrite_Throws()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        AspNetCoreEFCoreInvariantException ex = await Assert.ThrowsAsync<AspNetCoreEFCoreInvariantException>(
            () => invariants.AssertNoPartialWritesOnErrorAsync(
                static async (client, ct) =>
                    await client.PostAsync("/orders/rigged-failure", null, ct)));

        Assert.Contains("503", ex.Message);
        Assert.Contains("/orders/rigged-failure", ex.Message);
        Assert.Contains("Order", ex.Message);
        Assert.Contains("+1", ex.Message);
    }

    [Fact]
    public async Task Returns400_WithSavedAggregate_Throws()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        AspNetCoreEFCoreInvariantException ex = await Assert.ThrowsAsync<AspNetCoreEFCoreInvariantException>(
            () => invariants.AssertNoPartialWritesOnErrorAsync(
                static async (client, ct) =>
                    await client.PostAsync("/orders/rigged-400", null, ct)));

        Assert.Contains("400", ex.Message);
        Assert.Contains("/orders/rigged-400", ex.Message);
    }

    [Fact]
    public async Task NullRequestDelegate_Throws()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => invariants.AssertNoPartialWritesOnErrorAsync(null!));
    }

    [Fact]
    public void Constructor_NullHttpTarget_Throws()
    {
        IHost host = Host;
        AspNetCoreDbTarget<PartialWritesFixture.OrdersDb> db = new(host, "orders-db");

        Assert.Throws<ArgumentNullException>(
            () => new AspNetCoreEFCoreInvariants(null!, db));
    }

    [Fact]
    public void Constructor_NullDbTarget_Throws()
    {
        HttpClient client = fixture.Factory.CreateClient();
        HostHttpTarget http = new(Host, client);

        Assert.Throws<ArgumentNullException>(
            () => new AspNetCoreEFCoreInvariants(http, null!));
    }

    [Fact]
    public void AspNetCoreEFCoreInvariantException_IsDbInvariantException()
    {
        bool assignable = typeof(DbInvariantException).IsAssignableFrom(typeof(AspNetCoreEFCoreInvariantException));
        Assert.True(assignable);
    }
}
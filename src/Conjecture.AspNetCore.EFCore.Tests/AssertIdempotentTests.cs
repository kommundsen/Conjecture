// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.AspNetCore;
using Conjecture.AspNetCore.EFCore;
using Conjecture.EFCore;
using Conjecture.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore.EFCore.Tests;

internal sealed class IdempotentTestFixture : IDisposable
{
    private readonly SqliteConnection keepalive;

    private int statusFlipCounter;

    public string ConnectionString { get; }
    public WebApplicationFactory<TestApp> Factory { get; }

    public IdempotentTestFixture()
    {
        string dbName = $"idempotent-{Guid.NewGuid():N}";
        ConnectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";

        keepalive = new(ConnectionString);
        keepalive.Open();

        Factory = new WebApplicationFactory<TestApp>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddDbContext<IdempotentDbContext>(opts =>
                    opts.UseSqlite(ConnectionString));
                services.AddSingleton(this);
            });

            builder.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    // PUT /orders/{id} — idempotent upsert
                    endpoints.MapPut("/orders/{id:int}", async context =>
                    {
                        int id = int.Parse((string)context.Request.RouteValues["id"]!);
                        IdempotentDbContext db = context.RequestServices.GetRequiredService<IdempotentDbContext>();
                        IdempotentOrder? existing = await db.Orders.FindAsync(id);
                        if (existing is null)
                        {
                            db.Orders.Add(new IdempotentOrder { Id = id, Name = "Upserted" });
                        }
                        else
                        {
                            existing.Name = "Upserted";
                        }

                        await db.SaveChangesAsync();
                        context.Response.StatusCode = 200;
                    });

                    // DELETE /orders/{id} — idempotent delete (no-op if already gone)
                    endpoints.MapDelete("/orders/{id:int}", async context =>
                    {
                        int id = int.Parse((string)context.Request.RouteValues["id"]!);
                        IdempotentDbContext db = context.RequestServices.GetRequiredService<IdempotentDbContext>();
                        IdempotentOrder? existing = await db.Orders.FindAsync(id);
                        if (existing is not null)
                        {
                            db.Orders.Remove(existing);
                            await db.SaveChangesAsync();
                        }

                        context.Response.StatusCode = 200;
                    });

                    // POST /orders — NOT idempotent: always inserts a new row
                    endpoints.MapPost("/orders", async context =>
                    {
                        IdempotentDbContext db = context.RequestServices.GetRequiredService<IdempotentDbContext>();
                        db.Orders.Add(new IdempotentOrder { Name = "New" });
                        await db.SaveChangesAsync();
                        context.Response.StatusCode = 201;
                    });

                    // GET /orders/status-flip/{id} — flips 200/500 on each call
                    endpoints.MapGet("/orders/status-flip/{id:int}", context =>
                    {
                        IdempotentTestFixture self = context.RequestServices.GetRequiredService<IdempotentTestFixture>();
                        int count = System.Threading.Interlocked.Increment(ref self.statusFlipCounter);
                        context.Response.StatusCode = count % 2 == 1 ? 200 : 500;
                        return Task.CompletedTask;
                    });
                });
            });
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        IdempotentDbContext ctx = scope.ServiceProvider.GetRequiredService<IdempotentDbContext>();
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Factory.Dispose();
        keepalive.Dispose();
    }

    internal sealed class IdempotentOrder
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    internal sealed class IdempotentDbContext(DbContextOptions<IdempotentDbContext> opts) : DbContext(opts)
    {
        public DbSet<IdempotentOrder> Orders => Set<IdempotentOrder>();
    }
}

public sealed class AssertIdempotentTests : IDisposable
{
    private readonly IdempotentTestFixture fixture;

    public AssertIdempotentTests()
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
        AspNetCoreDbTarget<IdempotentTestFixture.IdempotentDbContext> db = new(Host, "idempotent-db");
        return new AspNetCoreEFCoreInvariants(http, db);
    }

    private int SeedOrder()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        IdempotentTestFixture.IdempotentDbContext ctx = scope.ServiceProvider.GetRequiredService<IdempotentTestFixture.IdempotentDbContext>();
        IdempotentTestFixture.IdempotentOrder order = new() { Name = "Existing" };
        ctx.Orders.Add(order);
        ctx.SaveChanges();
        return order.Id;
    }

    private static DiscoveredEndpoint MakeEndpoint(string httpMethod, string routePattern)
    {
        return new DiscoveredEndpoint(
            DisplayName: $"{httpMethod} {routePattern}",
            HttpMethod: httpMethod,
            RoutePattern: RoutePatternFactory.Parse(routePattern),
            Parameters: [],
            ProducesContentTypes: [],
            ConsumesContentTypes: [],
            RequiresAuthorization: false,
            Metadata: new Microsoft.AspNetCore.Http.EndpointMetadataCollection());
    }

    [Fact]
    public async Task Idempotent_PUT_TwoSequentialCalls_NoDiff_Passes()
    {
        int id = SeedOrder();
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();
        invariants.MarkIdempotent(static _ => true);

        DiscoveredEndpoint endpoint = MakeEndpoint("PUT", "/orders/{id}");

        await invariants.AssertIdempotentAsync(
            async (client, ct) =>
                await client.PutAsync(
                    FormattableString.Invariant($"/orders/{id}"), null, ct),
            endpoint);
    }

    [Fact]
    public async Task Idempotent_DELETE_TwoSequentialCalls_DataAlreadyGone_Passes()
    {
        int id = SeedOrder();
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();
        invariants.MarkIdempotent(static _ => true);

        DiscoveredEndpoint endpoint = MakeEndpoint("DELETE", "/orders/{id}");

        await invariants.AssertIdempotentAsync(
            async (client, ct) =>
                await client.DeleteAsync(
                    FormattableString.Invariant($"/orders/{id}"), ct),
            endpoint);
    }

    [Fact]
    public async Task MarkedIdempotent_ButPostInsertsNewRowEachTime_Throws()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();
        invariants.MarkIdempotent(static _ => true);

        DiscoveredEndpoint endpoint = MakeEndpoint("POST", "/orders");

        AspNetCoreEFCoreInvariantException ex = await Assert.ThrowsAsync<AspNetCoreEFCoreInvariantException>(
            () => invariants.AssertIdempotentAsync(
                static async (client, ct) =>
                    await client.PostAsync("/orders", null, ct),
                endpoint));

        Assert.Contains("POST", ex.Message);
        Assert.Contains("/orders", ex.Message);
        Assert.Contains("Order", ex.Message);
    }

    [Fact]
    public async Task NotMarkedIdempotent_AssertionSkipsSilently()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();
        invariants.MarkIdempotent(static _ => false);

        DiscoveredEndpoint endpoint = MakeEndpoint("POST", "/orders");

        // Should not throw even though POST inserts a new row each time.
        await invariants.AssertIdempotentAsync(
            static async (client, ct) =>
                await client.PostAsync("/orders", null, ct),
            endpoint);
    }

    [Fact]
    public async Task MarkedIdempotent_ButResponseCodeDiffers_Throws()
    {
        int id = SeedOrder();
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();
        invariants.MarkIdempotent(static _ => true);

        DiscoveredEndpoint endpoint = MakeEndpoint("GET", "/orders/status-flip/{id}");

        await Assert.ThrowsAsync<AspNetCoreEFCoreInvariantException>(
            () => invariants.AssertIdempotentAsync(
                async (client, ct) =>
                    await client.GetAsync(
                        FormattableString.Invariant($"/orders/status-flip/{id}"), ct),
                endpoint));
    }

    [Fact]
    public async Task NullRequest_Throws()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();
        invariants.MarkIdempotent(static _ => true);

        DiscoveredEndpoint endpoint = MakeEndpoint("GET", "/orders");

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => invariants.AssertIdempotentAsync(null!, endpoint));
    }

    [Fact]
    public async Task NullEndpoint_Throws()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();
        invariants.MarkIdempotent(static _ => true);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => invariants.AssertIdempotentAsync(
                static async (client, ct) =>
                    await client.GetAsync("/orders", ct),
                null!));
    }

    [Fact]
    public void MarkIdempotent_NullPredicate_Throws()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        Assert.Throws<ArgumentNullException>(
            () => invariants.MarkIdempotent(null!));
    }

    [Fact]
    public void MarkIdempotent_ReturnsThis_ForChaining()
    {
        AspNetCoreEFCoreInvariants invariants = CreateInvariants();

        AspNetCoreEFCoreInvariants result = invariants.MarkIdempotent(static _ => true);

        Assert.Same(invariants, result);
    }
}
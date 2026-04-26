// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore.Tests;

/// <summary>
/// Minimal entry point for the test host used by <see cref="DualEndpointWalkerTests"/>.
/// </summary>
public class DualEndpointWalkerTestsApp
{
}

public sealed class DualEndpointWalkerTests : IClassFixture<WebApplicationFactory<DualEndpointWalkerTestsApp>>
{
    private readonly WebApplicationFactory<DualEndpointWalkerTestsApp> factory;

    public DualEndpointWalkerTests(WebApplicationFactory<DualEndpointWalkerTestsApp> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseTestServer();

            builder.ConfigureServices(services =>
            {
                services.AddControllers()
                        .AddApplicationPart(typeof(DualEndpointWalkerTests).Assembly);
                services.AddEndpointsApiExplorer();
            });

            builder.Configure(app =>
            {
                app.UseRouting();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/items/{id:int}", (int id) => Results.Ok(id));
                    endpoints.MapGet("/items/{id:int}/search", (int id, string filter) => Results.Ok(id))
                             .WithName("ItemSearch");
                    endpoints.MapControllers();
                });
            });
        });
    }

    [Fact]
    public void Discover_WithMinimalApiAndController_ReturnsBothEndpoints()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        DualEndpointWalker walker = new(host);

        IReadOnlyList<DiscoveredEndpoint> discovered = walker.Discover();

        Assert.True(
            discovered.Count >= 2,
            $"Expected at least 2 endpoints (minimal API GET + MVC POST) but got {discovered.Count}.");
    }

    [Fact]
    public void Discover_MinimalApiGetEndpoint_ExtractsRouteParameterIdWithPathBindingSource()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        DualEndpointWalker walker = new(host);

        IReadOnlyList<DiscoveredEndpoint> discovered = walker.Discover();

        DiscoveredEndpoint? getEndpoint = discovered
            .FirstOrDefault(e =>
                e.HttpMethod.Equals("GET", System.StringComparison.OrdinalIgnoreCase) &&
                e.RoutePattern.RawText is not null &&
                e.RoutePattern.RawText.Contains("/items/", System.StringComparison.OrdinalIgnoreCase) &&
                e.RoutePattern.Parameters.Count == 1);

        Assert.NotNull(getEndpoint);
        EndpointParameter? idParam = getEndpoint.Parameters
            .FirstOrDefault(p => p.Name.Equals("id", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(idParam);
        Assert.Equal(typeof(int), idParam.ClrType);
        Assert.Equal(BindingSource.Path, idParam.Source);
    }

    [Fact]
    public void Discover_MvcPostEndpoint_ExtractsBodyParameterWithBodyBindingSource()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        DualEndpointWalker walker = new(host);

        IReadOnlyList<DiscoveredEndpoint> discovered = walker.Discover();

        DiscoveredEndpoint? postEndpoint = discovered
            .FirstOrDefault(e =>
                e.HttpMethod.Equals("POST", System.StringComparison.OrdinalIgnoreCase) &&
                e.RoutePattern.RawText is not null &&
                e.RoutePattern.RawText.Contains("/orders", System.StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(postEndpoint);
        EndpointParameter? bodyParam = postEndpoint.Parameters
            .FirstOrDefault(p => p.Source == BindingSource.Body);
        Assert.NotNull(bodyParam);
        Assert.Equal(typeof(CreateOrderRequest), bodyParam.ClrType);
    }

    [Fact]
    public void Discover_SameRouteAppearsInBothSources_NoDuplicatesInResult()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        DualEndpointWalker walker = new(host);

        IReadOnlyList<DiscoveredEndpoint> discovered = walker.Discover();

        IEnumerable<(string? Route, string Method)> keys = discovered
            .Select(e => (e.RoutePattern.RawText, e.HttpMethod));

        bool hasDuplicates = keys
            .GroupBy(k => k)
            .Any(g => g.Count() > 1);

        Assert.False(hasDuplicates, "Discover() returned duplicate (RoutePattern, HttpMethod) pairs.");
    }

    [Fact]
    public void Discover_AuthorizedMvcEndpoint_RequiresAuthorizationIsTrue()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        DualEndpointWalker walker = new(host);

        IReadOnlyList<DiscoveredEndpoint> discovered = walker.Discover();

        DiscoveredEndpoint? authorizedEndpoint = discovered
            .FirstOrDefault(e =>
                e.RoutePattern.RawText is not null &&
                e.RoutePattern.RawText.Contains("/protected", System.StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(authorizedEndpoint);
        Assert.True(authorizedEndpoint.RequiresAuthorization, "Endpoint decorated with [Authorize] must surface RequiresAuthorization = true.");
    }

    [Fact]
    public void Discover_AnonymousEndpoint_RequiresAuthorizationIsFalse()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        DualEndpointWalker walker = new(host);

        IReadOnlyList<DiscoveredEndpoint> discovered = walker.Discover();

        DiscoveredEndpoint? anonymousEndpoint = discovered
            .FirstOrDefault(e =>
                e.HttpMethod.Equals("GET", System.StringComparison.OrdinalIgnoreCase) &&
                e.RoutePattern.RawText is not null &&
                e.RoutePattern.RawText.Contains("/items/", System.StringComparison.OrdinalIgnoreCase) &&
                e.RoutePattern.Parameters.Count == 1);

        Assert.NotNull(anonymousEndpoint);
        Assert.False(anonymousEndpoint.RequiresAuthorization, "Unannotated endpoint must surface RequiresAuthorization = false.");
    }

    [Fact]
    public void Discover_EndpointWithRouteAndQueryParam_BothSurfaceWithCorrectBindingSources()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        DualEndpointWalker walker = new(host);

        IReadOnlyList<DiscoveredEndpoint> discovered = walker.Discover();

        // /items/{id:int}/search maps (int id, string filter) — id is Path, filter is Query
        DiscoveredEndpoint? searchEndpoint = discovered
            .FirstOrDefault(e =>
                e.HttpMethod.Equals("GET", System.StringComparison.OrdinalIgnoreCase) &&
                e.RoutePattern.RawText is not null &&
                e.RoutePattern.RawText.Contains("/search", System.StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(searchEndpoint);

        EndpointParameter? routeParam = searchEndpoint.Parameters
            .FirstOrDefault(p => p.Name.Equals("id", System.StringComparison.OrdinalIgnoreCase));
        EndpointParameter? queryParam = searchEndpoint.Parameters
            .FirstOrDefault(p => p.Name.Equals("filter", System.StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(routeParam);
        Assert.Equal(BindingSource.Path, routeParam.Source);

        Assert.NotNull(queryParam);
        Assert.Equal(BindingSource.Query, queryParam.Source);
    }
}

// ---------------------------------------------------------------------------
// Test support: MVC controllers used by the test host
// ---------------------------------------------------------------------------

[ApiController]
public class OrdersController : ControllerBase
{
    [HttpPost("/orders")]
    public IActionResult Create([FromBody] CreateOrderRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);
        return Ok();
    }
}

[ApiController]
public class ProtectedController : ControllerBase
{
    [Authorize]
    [HttpGet("/protected")]
    public IActionResult GetSecret() => Ok("secret");
}

public sealed record CreateOrderRequest(string ProductId, int Quantity);

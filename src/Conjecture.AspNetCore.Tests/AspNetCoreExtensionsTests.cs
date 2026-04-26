// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Net.Http;

using Conjecture.AspNetCore;
using Conjecture.Core;
using Conjecture.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore.Tests;

public sealed class AspNetCoreExtensionsTests : IClassFixture<WebApplicationFactory<AspNetCoreExtensionsTestsApp>>
{
    private readonly WebApplicationFactory<AspNetCoreExtensionsTestsApp> factory;

    public AspNetCoreExtensionsTests(WebApplicationFactory<AspNetCoreExtensionsTestsApp> factory)
    {
        this.factory = factory.WithWebHostBuilder(static webBuilder =>
        {
            webBuilder.Configure(static app =>
            {
                app.UseRouting();
                app.UseEndpoints(static endpoints =>
                {
                    endpoints.MapGet("/ping", static () => Results.Ok("pong"));
                });
            });
        });
    }

    [Fact]
    public void AspNetCoreRequests_ReturnsBuilder()
    {
        HttpClient client = factory.CreateClient();
        IHost host = factory.Services.GetRequiredService<IHost>();

        AspNetCoreRequestBuilder builder = Generate.AspNetCoreRequests(host, client);

        Assert.NotNull(builder);
    }

    [Fact]
    public void AspNetCoreRequests_BuildsStrategy()
    {
        HttpClient client = factory.CreateClient();
        IHost host = factory.Services.GetRequiredService<IHost>();

        Strategy<HttpInteraction> strategy = Generate
            .AspNetCoreRequests(host, client)
            .ValidRequestsOnly()
            .Build();

        Assert.NotNull(strategy);
    }

    [Fact]
    public void AspNetCoreRequests_NullHost_Throws()
    {
        HttpClient client = factory.CreateClient();

        Assert.Throws<ArgumentNullException>(() => Generate.AspNetCoreRequests(null!, client));
    }

    [Fact]
    public void AspNetCoreRequests_NullClient_Throws()
    {
        IHost host = factory.Services.GetRequiredService<IHost>();

        Assert.Throws<ArgumentNullException>(() => Generate.AspNetCoreRequests(host, null!));
    }
}

public sealed class AspNetCoreExtensionsTestsApp
{
}

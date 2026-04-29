// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Net.Http;
using System.Threading.Tasks;

using Conjecture.AspNetCore;
using Conjecture.AspNetCore.Tests.TestSupport;
using Conjecture.Core;
using Conjecture.Http;
using Conjecture.Interactions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore.Tests.Samples;

// Runnable xUnit v2 wiring sample — class-scoped WebApplicationFactory shared via
// IClassFixture<T>. The same shape works in xUnit v3 (replace IClassFixture<T> with
// IClassFixture<T> + IAsyncLifetime; v3 keeps the same fixture contract).
public sealed class XunitSample(WebApplicationFactory<XunitSampleApp> factory) : IClassFixture<WebApplicationFactory<XunitSampleApp>>
{
    private readonly WebApplicationFactory<XunitSampleApp> factory = factory.WithWebHostBuilder(static webBuilder => webBuilder.Configure(static app =>
            {
                app.UseRouting();
                app.UseEndpoints(static endpoints => endpoints.MapGet("/orders", static () => Results.Ok("orders")));
            }));

    [Fact]
    public async Task NoValidGetReturns5xx()
    {
        using HttpClient client = factory.CreateClient();
        IHost host = factory.Services.GetRequiredService<IHost>();
        HostHttpTarget target = new(host, client);

        Strategy<HttpInteraction> strategy = Strategy
            .AspNetCoreRequests(host, client)
            .ExcludeEndpoints(static ep => ep.HttpMethod is not "GET")
            .ValidRequestsOnly()
            .Build();

        await Property.ForAll(target, strategy, static async (t, request) =>
        {
            HttpResponseMessage response = await request.Response((IHttpTarget)t);
            await Task.FromResult(response).AssertNot5xx();
        }, ct: default);
    }
}

public sealed class XunitSampleApp
{
}
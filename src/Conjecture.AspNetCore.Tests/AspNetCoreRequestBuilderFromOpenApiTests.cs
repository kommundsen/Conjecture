// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using Conjecture.AspNetCore;
using Conjecture.AspNetCore.Tests.TestSupport;
using Conjecture.Core;
using Conjecture.Http;
using Conjecture.OpenApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore.Tests;

public sealed class AspNetCoreRequestBuilderFromOpenApiTests(WebApplicationFactory<AspNetCoreRequestBuilderFromOpenApiTestsApp> factory) : IClassFixture<WebApplicationFactory<AspNetCoreRequestBuilderFromOpenApiTestsApp>>
{
    private const string PetstoreJson = """
        {
          "openapi": "3.0.1",
          "info": { "title": "Petstore", "version": "1.0" },
          "paths": {
            "/orders": {
              "post": {
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "required": ["productId", "quantity"],
                        "properties": {
                          "productId": { "type": "string" },
                          "quantity":  { "type": "integer" }
                        }
                      }
                    }
                  }
                },
                "responses": {
                  "201": { "description": "Created" }
                }
              }
            }
          }
        }
        """;

    private readonly WebApplicationFactory<AspNetCoreRequestBuilderFromOpenApiTestsApp> factory = factory.WithWebHostBuilder(static webBuilder => webBuilder.Configure(static app =>
            {
                app.UseRouting();
                app.UseEndpoints(static endpoints => endpoints.MapPost("/orders", static (BuilderTestDto _) => Results.Ok()));
            }));

    [Fact]
    public async Task FromOpenApi_ReturnsBuilder()
    {
        OpenApiDocument doc = await LoadDocAsync();
        IHost host = factory.Services.GetRequiredService<IHost>();
        using HttpClient client = factory.CreateClient();

        AspNetCoreRequestBuilder builder = Strategy
            .AspNetCoreRequests(host, client)
            .FromOpenApi(doc);

        Assert.NotNull(builder);
    }

    [Fact]
    public async Task FromOpenApi_BuildsStrategy()
    {
        OpenApiDocument doc = await LoadDocAsync();
        IHost host = factory.Services.GetRequiredService<IHost>();
        using HttpClient client = factory.CreateClient();

        Strategy<HttpInteraction> strategy = Strategy
            .AspNetCoreRequests(host, client)
            .FromOpenApi(doc)
            .ValidRequestsOnly()
            .Build();

        Assert.NotNull(strategy);
    }

    [Fact]
    public async Task FromOpenApi_SynthesisesRequestsForMatchingEndpoints()
    {
        OpenApiDocument doc = await LoadDocAsync();
        IHost host = factory.Services.GetRequiredService<IHost>();
        using HttpClient client = factory.CreateClient();

        Strategy<HttpInteraction> strategy = Strategy
            .AspNetCoreRequests(host, client)
            .FromOpenApi(doc)
            .ValidRequestsOnly()
            .Build();

        // Walker discovers the /orders POST endpoint; FromOpenApi seeds bodies from the
        // doc when the walker classifies a body parameter (controllers via ApiExplorer).
        // For minimal-API delegates the walker emits only path + query bindings today,
        // so this test pins the cross-cutting behaviour: route + method are unchanged.
        IReadOnlyList<HttpInteraction> samples = strategy.WithSeed(1UL).Sample(5);

        foreach (HttpInteraction sample in samples)
        {
            Assert.Equal("POST", sample.Method);
            Assert.Equal("/orders", sample.Path);
        }
    }

    [Fact]
    public async Task FromOpenApi_NullDoc_Throws()
    {
        IHost host = factory.Services.GetRequiredService<IHost>();
        using HttpClient client = factory.CreateClient();

        AspNetCoreRequestBuilder builder = Strategy.AspNetCoreRequests(host, client);

        await Task.Yield();
        Assert.Throws<ArgumentNullException>(() => builder.FromOpenApi(null!));
    }

    private static async Task<OpenApiDocument> LoadDocAsync()
    {
        string path = Path.GetTempFileName() + ".json";
        await File.WriteAllTextAsync(path, PetstoreJson);
        try
        {
            return await Strategy.FromOpenApi(path);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

public sealed class AspNetCoreRequestBuilderFromOpenApiTestsApp
{
}
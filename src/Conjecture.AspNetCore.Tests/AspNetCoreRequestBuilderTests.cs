// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Core;
using Conjecture.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore.Tests;

/// <summary>
/// Minimal entry point class for the <see cref="AspNetCoreRequestBuilderTests"/> host.
/// WebApplicationFactory resolves this class to create the test host.
/// </summary>
public class AspNetCoreRequestBuilderTestsApp
{
}

/// <summary>
/// Tests for the fluent <see cref="AspNetCoreRequestBuilder"/> — cycle 5 of #290.
///
/// CONSTRAINT FOR DEVELOPER: <see cref="DiscoveredEndpoint"/> is currently internal.
/// The <see cref="AspNetCoreRequestBuilder.ExcludeEndpoints"/> method signature
/// takes <c>Func&lt;DiscoveredEndpoint, bool&gt;</c> per ADR 0063, so
/// <see cref="DiscoveredEndpoint"/> (and <see cref="EndpointParameter"/>) must be
/// promoted to <c>public</c> in cycle 5. PublicAPI.Unshipped.txt must declare the
/// full signatures of both types before the build will pass.
/// </summary>
public sealed class AspNetCoreRequestBuilderTests : IClassFixture<WebApplicationFactory<AspNetCoreRequestBuilderTestsApp>>
{
    private readonly WebApplicationFactory<AspNetCoreRequestBuilderTestsApp> factory;

    public AspNetCoreRequestBuilderTests(WebApplicationFactory<AspNetCoreRequestBuilderTestsApp> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseTestServer();

            builder.ConfigureServices(services =>
            {
                services.AddEndpointsApiExplorer();
                services.AddRouting();
            });

            builder.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/public", static () => Results.Ok("public"));
                    endpoints.MapGet("/admin", static () => Results.Ok("admin"));
                });
            });
        });
    }

    // ---------------------------------------------------------------------------
    // Test 1 — Constructor accepts IHost + HttpClient
    // ---------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithHostAndClient_SucceedsWithoutException()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        HttpClient client = this.factory.CreateClient();

        AspNetCoreRequestBuilder builder = new(host, client);

        Assert.NotNull(builder);
    }

    // ---------------------------------------------------------------------------
    // Test 2 — ExcludeEndpoints filters matching routes
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExcludeEndpoints_AdminPredicate_NeverSendsToAdmin()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        HttpClient client = this.factory.CreateClient();

        Strategy<HttpInteraction> strategy = new AspNetCoreRequestBuilder(host, client)
            .ExcludeEndpoints(static ep => ep.RoutePattern.RawText == "/admin")
            .Build();

        IReadOnlyList<HttpInteraction> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        Assert.All(samples, interaction =>
            Assert.DoesNotContain("/admin", interaction.Path, StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------
    // Test 3 — Multiple ExcludeEndpoints calls AND together
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExcludeEndpoints_MultiplePredicates_AllPredicatesApplied()
    {
        WebApplicationFactory<AspNetCoreRequestBuilderTestsApp> localFactory =
            this.factory.WithWebHostBuilder(builder =>
            {
                builder.UseTestServer();

                builder.ConfigureServices(services =>
                {
                    services.AddEndpointsApiExplorer();
                    services.AddRouting();
                });

                builder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/a", static () => Results.Ok("a"));
                        endpoints.MapGet("/b", static () => Results.Ok("b"));
                        endpoints.MapGet("/c", static () => Results.Ok("c"));
                    });
                });
            });

        IHost host = localFactory.Services.GetRequiredService<IHost>();
        HttpClient client = localFactory.CreateClient();

        Strategy<HttpInteraction> strategy = new AspNetCoreRequestBuilder(host, client)
            .ExcludeEndpoints(static ep => ep.RoutePattern.RawText == "/a")
            .ExcludeEndpoints(static ep => ep.RoutePattern.RawText == "/b")
            .Build();

        IReadOnlyList<HttpInteraction> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        Assert.All(samples, interaction => Assert.Equal("/c", interaction.Path));
    }

    // ---------------------------------------------------------------------------
    // Test 4 — WithSetup runs before each example
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithSetup_Delegate_CalledBeforeEachExample()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        HttpClient client = this.factory.CreateClient();

        int callCount = 0;

        Strategy<HttpInteraction> strategy = new AspNetCoreRequestBuilder(host, client)
            .WithSetup(() =>
            {
                Interlocked.Increment(ref callCount);
                return Task.CompletedTask;
            })
            .Build();

        int sampleCount = 10;
        DataGen.Sample(strategy, count: sampleCount, seed: 1UL);

        Assert.Equal(sampleCount, callCount);
    }

    // ---------------------------------------------------------------------------
    // Test 5 — ValidRequestsOnly suppresses malformed flavour
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidRequestsOnly_AllExamplesAreValid()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        HttpClient client = this.factory.CreateClient();

        Strategy<HttpInteraction> strategy = new AspNetCoreRequestBuilder(host, client)
            .ValidRequestsOnly()
            .Build();

        IReadOnlyList<HttpInteraction> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        // Valid interactions must not contain the malformed-body marker.
        Assert.All(
            samples,
            interaction => Assert.False(
                interaction.Body is string s && s.Contains("invalid json", StringComparison.OrdinalIgnoreCase),
                "ValidRequestsOnly must not emit malformed requests."));
    }

    // ---------------------------------------------------------------------------
    // Test 6 — MalformedRequestsOnly suppresses valid flavour
    // This test uses a minimal-API host with a body-parameter endpoint so
    // valid vs malformed is observable from the generated interaction.
    // ---------------------------------------------------------------------------

    [Fact]
    public void MalformedRequestsOnly_AllExamplesAreMalformed()
    {
        WebApplicationFactory<AspNetCoreRequestBuilderTestsApp> localFactory =
            this.factory.WithWebHostBuilder(builder =>
            {
                builder.UseTestServer();

                builder.ConfigureServices(services =>
                {
                    services.AddEndpointsApiExplorer();
                    services.AddRouting();
                });

                builder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/submit", static (BuilderTestDto _) => Results.Ok());
                    });
                });
            });

        IHost host = localFactory.Services.GetRequiredService<IHost>();
        HttpClient client = localFactory.CreateClient();

        Strategy<HttpInteraction> strategy = new AspNetCoreRequestBuilder(host, client)
            .MalformedRequestsOnly()
            .Build();

        IReadOnlyList<HttpInteraction> samples = DataGen.Sample(strategy, count: 50, seed: 1UL);

        // Every interaction generated under MalformedRequestsOnly must carry a signal
        // that identifies it as malformed: wrong Content-Type, malformed JSON body,
        // or null body with JSON Content-Type header (three flavours from RequestSynthesizer).
        Assert.All(
            samples,
            interaction =>
            {
                bool hasMalformedBody =
                    interaction.Body is string s &&
                    s.Contains("invalid json", StringComparison.OrdinalIgnoreCase);

                bool hasWrongContentType =
                    interaction.Headers is not null &&
                    interaction.Headers.TryGetValue("Content-Type", out string? ct) &&
                    string.Equals(ct, "text/plain", StringComparison.OrdinalIgnoreCase);

                bool hasNullBodyWithJsonHeader =
                    interaction.Body is null &&
                    interaction.Headers is not null &&
                    interaction.Headers.TryGetValue("Content-Type", out string? ct2) &&
                    string.Equals(ct2, "application/json", StringComparison.OrdinalIgnoreCase);

                Assert.True(
                    hasMalformedBody || hasWrongContentType || hasNullBodyWithJsonHeader,
                    "MalformedRequestsOnly must only emit malformed interactions.");
            });
    }

    // ---------------------------------------------------------------------------
    // Test 7 — Default emits both valid and malformed flavours
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_Default_EmitsBothValidAndMalformedFlavours()
    {
        WebApplicationFactory<AspNetCoreRequestBuilderTestsApp> localFactory =
            this.factory.WithWebHostBuilder(builder =>
            {
                builder.UseTestServer();

                builder.ConfigureServices(services =>
                {
                    services.AddEndpointsApiExplorer();
                    services.AddRouting();
                });

                builder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/orders", static (BuilderTestDto _) => Results.Ok());
                    });
                });
            });

        IHost host = localFactory.Services.GetRequiredService<IHost>();
        HttpClient client = localFactory.CreateClient();

        Strategy<HttpInteraction> strategy = new AspNetCoreRequestBuilder(host, client)
            .Build();

        IReadOnlyList<HttpInteraction> samples = DataGen.Sample(strategy, count: 100, seed: 42UL);

        int malformedCount = samples.Count(static interaction =>
            (interaction.Body is string s && s.Contains("invalid json", StringComparison.OrdinalIgnoreCase)) ||
            (interaction.Headers is not null &&
             interaction.Headers.TryGetValue("Content-Type", out string? ct) &&
             string.Equals(ct, "text/plain", StringComparison.OrdinalIgnoreCase)) ||
            (interaction.Body is null &&
             interaction.Headers is not null &&
             interaction.Headers.TryGetValue("Content-Type", out string? ct2) &&
             string.Equals(ct2, "application/json", StringComparison.OrdinalIgnoreCase)));

        int validCount = samples.Count - malformedCount;

        // Neither flavour should dominate completely — neither should exceed 90 of 100.
        Assert.True(malformedCount > 0, "Default build must emit some malformed interactions.");
        Assert.True(validCount > 0, "Default build must emit some valid interactions.");
        Assert.True(malformedCount <= 90, $"Malformed flavour dominated: {malformedCount}/100 ≥ 90.");
        Assert.True(validCount <= 90, $"Valid flavour dominated: {validCount}/100 ≥ 90.");
    }

    // ---------------------------------------------------------------------------
    // Test 8 — Build returns Strategy<HttpInteraction>
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ReturnsStrategyOfHttpInteraction()
    {
        IHost host = this.factory.Services.GetRequiredService<IHost>();
        HttpClient client = this.factory.CreateClient();

        Strategy<HttpInteraction> strategy = new AspNetCoreRequestBuilder(host, client)
            .Build();

        Assert.NotNull(strategy);
    }
}

// ---------------------------------------------------------------------------
// Test support types for this test class
// ---------------------------------------------------------------------------

/// <summary>Simple DTO for builder tests that need a body-parameter endpoint.</summary>
public sealed record BuilderTestDto(string Name);

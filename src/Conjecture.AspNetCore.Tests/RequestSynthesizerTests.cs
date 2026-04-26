// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq;

using Conjecture.Core;
using Conjecture.Http;

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Conjecture.AspNetCore.Tests;

/// <summary>
/// Tests for <see cref="RequestSynthesizer"/>. All tests operate against
/// <see cref="DiscoveredEndpoint"/> instances built inline — no live host required.
/// </summary>
public sealed class RequestSynthesizerTests
{
    // ---------------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------------

    private static DiscoveredEndpoint MakePostOrdersEndpoint() =>
        new(
            DisplayName: "POST /orders",
            HttpMethod: "POST",
            RoutePattern: RoutePatternFactory.Parse("/orders"),
            Parameters:
            [
                new EndpointParameter(
                    Name: "request",
                    ClrType: typeof(CreateOrderRequest),
                    Source: BindingSource.Body,
                    IsRequired: true),
            ],
            ProducesContentTypes: ["application/json"],
            ConsumesContentTypes: ["application/json"],
            RequiresAuthorization: false,
            Metadata: new([]));

    private static DiscoveredEndpoint MakeGetOrderByIdEndpoint() =>
        new(
            DisplayName: "GET /orders/{id}",
            HttpMethod: "GET",
            RoutePattern: RoutePatternFactory.Parse("/orders/{id}"),
            Parameters:
            [
                new EndpointParameter(
                    Name: "id",
                    ClrType: typeof(int),
                    Source: BindingSource.Path,
                    IsRequired: true),
            ],
            ProducesContentTypes: ["application/json"],
            ConsumesContentTypes: [],
            RequiresAuthorization: false,
            Metadata: new([]));

    private static DiscoveredEndpoint MakeGetWithQueryEndpoint() =>
        new(
            DisplayName: "GET /orders",
            HttpMethod: "GET",
            RoutePattern: RoutePatternFactory.Parse("/orders"),
            Parameters:
            [
                new EndpointParameter(
                    Name: "status",
                    ClrType: typeof(string),
                    Source: BindingSource.Query,
                    IsRequired: true),
            ],
            ProducesContentTypes: ["application/json"],
            ConsumesContentTypes: [],
            RequiresAuthorization: false,
            Metadata: new([]));

    private static DiscoveredEndpoint MakeEndpointWithUnknownType() =>
        new(
            DisplayName: "POST /unknown",
            HttpMethod: "POST",
            RoutePattern: RoutePatternFactory.Parse("/unknown"),
            Parameters:
            [
                new EndpointParameter(
                    Name: "body",
                    ClrType: typeof(UnregisteredBodyType),
                    Source: BindingSource.Body,
                    IsRequired: true),
            ],
            ProducesContentTypes: [],
            ConsumesContentTypes: ["application/json"],
            RequiresAuthorization: false,
            Metadata: new([]));

    // ---------------------------------------------------------------------------
    // Valid strategy — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidStrategy_PostWithJsonBody_ProducesApplicationJsonContentType()
    {
        DiscoveredEndpoint endpoint = MakePostOrdersEndpoint();
        RequestSynthesizer synthesizer = new(endpoint);
        Strategy<HttpInteraction> strategy = synthesizer.ValidStrategy();

        IReadOnlyList<HttpInteraction> samples = DataGen.Sample(strategy, count: 10, seed: 1UL);

        Assert.All(samples, interaction =>
        {
            Assert.True(
                interaction.Headers is not null &&
                interaction.Headers.TryGetValue("Content-Type", out string? ct) &&
                ct is not null &&
                ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase),
                $"Expected Content-Type: application/json but got: {interaction.Headers?.GetValueOrDefault("Content-Type")}");
        });
    }

    [Fact]
    public void ValidStrategy_PostWithJsonBody_ProducesParseableJsonBody()
    {
        DiscoveredEndpoint endpoint = MakePostOrdersEndpoint();
        RequestSynthesizer synthesizer = new(endpoint);
        Strategy<HttpInteraction> strategy = synthesizer.ValidStrategy();

        IReadOnlyList<HttpInteraction> samples = DataGen.Sample(strategy, count: 10, seed: 1UL);

        Assert.All(samples, interaction =>
        {
            Assert.NotNull(interaction.Body);
        });
    }

    [Fact]
    public void ValidStrategy_PostWithJsonBody_ProducesPostMethod()
    {
        DiscoveredEndpoint endpoint = MakePostOrdersEndpoint();
        RequestSynthesizer synthesizer = new(endpoint);
        Strategy<HttpInteraction> strategy = synthesizer.ValidStrategy();

        IReadOnlyList<HttpInteraction> samples = DataGen.Sample(strategy, count: 10, seed: 1UL);

        Assert.All(samples, interaction =>
        {
            Assert.Equal("POST", interaction.Method, StringComparer.OrdinalIgnoreCase);
        });
    }

    // ---------------------------------------------------------------------------
    // Valid strategy — route parameter interpolation
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidStrategy_EndpointWithIntRouteParam_SubstitutesValueInPath()
    {
        DiscoveredEndpoint endpoint = MakeGetOrderByIdEndpoint();
        RequestSynthesizer synthesizer = new(endpoint);
        Strategy<HttpInteraction> strategy = synthesizer.ValidStrategy();

        IReadOnlyList<HttpInteraction> samples = DataGen.Sample(strategy, count: 10, seed: 1UL);

        Assert.All(samples, interaction =>
        {
            // The path must not contain a literal "{id}" placeholder.
            Assert.DoesNotContain("{id}", interaction.Path, StringComparison.OrdinalIgnoreCase);
            // The path must start with /orders/ followed by a numeric segment.
            Assert.StartsWith("/orders/", interaction.Path, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ---------------------------------------------------------------------------
    // Valid strategy — query string composition
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidStrategy_EndpointWithRequiredQueryParam_IncludesQueryStringInPath()
    {
        DiscoveredEndpoint endpoint = MakeGetWithQueryEndpoint();
        RequestSynthesizer synthesizer = new(endpoint);
        Strategy<HttpInteraction> strategy = synthesizer.ValidStrategy();

        IReadOnlyList<HttpInteraction> samples = DataGen.Sample(strategy, count: 10, seed: 1UL);

        Assert.All(samples, interaction =>
        {
            Assert.Contains("status=", interaction.Path, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ---------------------------------------------------------------------------
    // Valid strategy — primitive types do not throw
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(System.Guid))]
    public void ValidStrategy_PrimitiveRouteParam_DoesNotThrow(Type primitiveType)
    {
        DiscoveredEndpoint endpoint = new(
            DisplayName: $"GET /resource/{{{primitiveType.Name}}}",
            HttpMethod: "GET",
            RoutePattern: RoutePatternFactory.Parse($"/resource/{{value}}"),
            Parameters:
            [
                new EndpointParameter(
                    Name: "value",
                    ClrType: primitiveType,
                    Source: BindingSource.Path,
                    IsRequired: true),
            ],
            ProducesContentTypes: [],
            ConsumesContentTypes: [],
            RequiresAuthorization: false,
            Metadata: new([]));

        RequestSynthesizer synthesizer = new(endpoint);
        Strategy<HttpInteraction> strategy = synthesizer.ValidStrategy();

        Assert.NotNull(strategy);
    }

    // ---------------------------------------------------------------------------
    // Malformed strategy — structural difference from valid
    // ---------------------------------------------------------------------------

    [Fact]
    public void MalformedStrategy_PostEndpoint_ProducesStructurallyDifferentInteraction()
    {
        DiscoveredEndpoint endpoint = MakePostOrdersEndpoint();
        RequestSynthesizer synthesizer = new(endpoint);
        Strategy<HttpInteraction> validStrategy = synthesizer.ValidStrategy();
        Strategy<HttpInteraction> malformedStrategy = synthesizer.MalformedStrategy();

        IReadOnlyList<HttpInteraction> validSamples = DataGen.Sample(validStrategy, count: 10, seed: 1UL);
        IReadOnlyList<HttpInteraction> malformedSamples = DataGen.Sample(malformedStrategy, count: 10, seed: 1UL);

        // At least one of the malformed interactions must differ from all valid ones in
        // Content-Type header OR body content.
        bool foundDifference = malformedSamples.Any(malformed =>
            validSamples.All(valid =>
            {
                string? validCt = valid.Headers?.GetValueOrDefault("Content-Type");
                string? malformedCt = malformed.Headers?.GetValueOrDefault("Content-Type");
                bool contentTypeDiffers = !string.Equals(validCt, malformedCt, StringComparison.OrdinalIgnoreCase);
                bool bodyDiffers = !Equals(valid.Body, malformed.Body);
                return contentTypeDiffers || bodyDiffers;
            }));

        Assert.True(foundDifference, "MalformedStrategy must produce at least one interaction that differs structurally from all valid interactions.");
    }

    // ---------------------------------------------------------------------------
    // Unknown type → throws at strategy-build time
    // ---------------------------------------------------------------------------

    [Fact]
    public void ValidStrategy_UnregisteredBodyType_ThrowsArgumentException()
    {
        DiscoveredEndpoint endpoint = MakeEndpointWithUnknownType();
        RequestSynthesizer synthesizer = new(endpoint);

        Action act = () => synthesizer.ValidStrategy();
        ArgumentException ex = Assert.Throws<ArgumentException>(act);

        // Message must mention the parameter name, endpoint display name, and missing registration.
        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("POST /unknown", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

// ---------------------------------------------------------------------------
// Marker type: has no [Arbitrary] / Generate.For<T>() registration — used to test
// that unknown types throw at strategy-build time.
// ---------------------------------------------------------------------------
internal sealed record UnregisteredBodyType(string Value);

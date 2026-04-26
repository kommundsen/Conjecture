// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Conjecture.AspNetCore;

/// <summary>Metadata for a single HTTP endpoint discovered from the ASP.NET Core routing infrastructure.</summary>
/// <param name="DisplayName">Human-readable name for the endpoint.</param>
/// <param name="HttpMethod">The HTTP method (e.g. GET, POST).</param>
/// <param name="RoutePattern">The parsed route pattern, including raw text and parameter descriptors.</param>
/// <param name="Parameters">Describes each path, query, and body parameter inferred from the route and binding metadata.</param>
/// <param name="ProducesContentTypes">The content types the endpoint can produce.</param>
/// <param name="ConsumesContentTypes">The content types the endpoint can consume.</param>
/// <param name="RequiresAuthorization">Whether the endpoint requires authorization.</param>
/// <param name="Metadata">The full endpoint metadata collection.</param>
public sealed record DiscoveredEndpoint(
    string DisplayName,
    string HttpMethod,
    RoutePattern RoutePattern,
    IReadOnlyList<EndpointParameter> Parameters,
    IReadOnlyList<string> ProducesContentTypes,
    IReadOnlyList<string> ConsumesContentTypes,
    bool RequiresAuthorization,
    EndpointMetadataCollection Metadata);
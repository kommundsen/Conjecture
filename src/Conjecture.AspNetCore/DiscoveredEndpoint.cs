// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Conjecture.AspNetCore;

internal sealed record DiscoveredEndpoint(
    string DisplayName,
    string HttpMethod,
    RoutePattern RoutePattern,
    IReadOnlyList<EndpointParameter> Parameters,
    IReadOnlyList<string> ProducesContentTypes,
    IReadOnlyList<string> ConsumesContentTypes,
    bool RequiresAuthorization,
    EndpointMetadataCollection Metadata);
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore;

internal sealed class DualEndpointWalker(IHost host)
{
    private static readonly Regex ConstraintPattern = new(@"\{(\w+):[^}]+\}", RegexOptions.Compiled);

    public IReadOnlyList<DiscoveredEndpoint> Discover()
    {
        List<DiscoveredEndpoint> fromDataSource = FromDataSource();
        List<DiscoveredEndpoint> fromApi = FromApiDescriptions();

        // Build a lookup from normalised (RoutePattern, HttpMethod) → ApiDescription descriptor.
        // ApiDescription entries carry richer metadata and are preferred when both sources cover
        // the same endpoint.
        Dictionary<(string Route, string Method), DiscoveredEndpoint> apiLookup =
            fromApi.ToDictionary(
                e => (NormalizePattern(e.RoutePattern.RawText ?? string.Empty), e.HttpMethod.ToUpperInvariant()),
                e => e);

        // Merge: prefer ApiDescription entry when present, fall back to EndpointDataSource.
        Dictionary<(string, string), DiscoveredEndpoint> merged = [];

        foreach (DiscoveredEndpoint ds in fromDataSource)
        {
            (string, string) key = (NormalizePattern(ds.RoutePattern.RawText ?? string.Empty), ds.HttpMethod.ToUpperInvariant());
            merged[key] = apiLookup.TryGetValue(key, out DiscoveredEndpoint? apiDescriptor)
                ? apiDescriptor
                : ds;
        }

        // Add any ApiDescription entries not already covered by EndpointDataSource.
        foreach (DiscoveredEndpoint api in fromApi)
        {
            (string, string) key = (NormalizePattern(api.RoutePattern.RawText ?? string.Empty), api.HttpMethod.ToUpperInvariant());
            if (!merged.ContainsKey(key))
            {
                merged[key] = api;
            }
        }

        return merged.Values.ToList();
    }

    private List<DiscoveredEndpoint> FromDataSource()
    {
        EndpointDataSource dataSource = host.Services.GetRequiredService<EndpointDataSource>();
        List<DiscoveredEndpoint> results = [];

        foreach (RouteEndpoint endpoint in dataSource.Endpoints.OfType<RouteEndpoint>())
        {
            IHttpMethodMetadata? httpMethodMetadata =
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();

            if (httpMethodMetadata is null || httpMethodMetadata.HttpMethods.Count == 0)
            {
                continue;
            }

            List<EndpointParameter> parameters = ExtractParametersFromDataSource(endpoint);

            bool requiresAuth = endpoint.Metadata.OfType<IAuthorizeData>().Any();

            foreach (string method in httpMethodMetadata.HttpMethods)
            {
                results.Add(new DiscoveredEndpoint(
                    DisplayName: endpoint.DisplayName ?? string.Empty,
                    HttpMethod: method.ToUpperInvariant(),
                    RoutePattern: endpoint.RoutePattern,
                    Parameters: parameters,
                    ProducesContentTypes: [],
                    ConsumesContentTypes: [],
                    RequiresAuthorization: requiresAuth,
                    Metadata: endpoint.Metadata));
            }
        }

        return results;
    }

    private static List<EndpointParameter> ExtractParametersFromDataSource(RouteEndpoint endpoint)
    {
        List<EndpointParameter> parameters = [];

        // The route handler delegate's MethodInfo is stored in the endpoint metadata by
        // the minimal API pipeline.
        MethodInfo? delegateMethod = endpoint.Metadata.OfType<MethodInfo>().FirstOrDefault();
        if (delegateMethod is null)
        {
            return parameters;
        }

        // Collect route parameter names from the route pattern (names only, no constraints).
        HashSet<string> routeParamNames = endpoint.RoutePattern.Parameters
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (ParameterInfo param in delegateMethod.GetParameters())
        {
            if (param.Name is null)
            {
                continue;
            }

            if (routeParamNames.Contains(param.Name))
            {
                parameters.Add(new EndpointParameter(
                    Name: param.Name,
                    ClrType: param.ParameterType,
                    Source: BindingSource.Path,
                    IsRequired: true));
            }
            else if (!param.ParameterType.IsClass || param.ParameterType == typeof(string))
            {
                // Simple types not in the route are treated as query parameters.
                parameters.Add(new EndpointParameter(
                    Name: param.Name,
                    ClrType: param.ParameterType,
                    Source: BindingSource.Query,
                    IsRequired: !param.HasDefaultValue));
            }
        }

        return parameters;
    }

    private List<DiscoveredEndpoint> FromApiDescriptions()
    {
        IApiDescriptionGroupCollectionProvider? provider =
            host.Services.GetService<IApiDescriptionGroupCollectionProvider>();

        if (provider is null)
        {
            return [];
        }

        // Build a lookup from endpoint display name → RouteEndpoint so we can retrieve the
        // full EndpointMetadataCollection (which carries IAuthorizeData).
        EndpointDataSource dataSource = host.Services.GetRequiredService<EndpointDataSource>();
        Dictionary<string, RouteEndpoint> endpointByName = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.DisplayName is not null)
            .ToDictionary(e => e.DisplayName!, e => e, StringComparer.OrdinalIgnoreCase);

        List<DiscoveredEndpoint> results = [];

        foreach (ApiDescriptionGroup group in provider.ApiDescriptionGroups.Items)
        {
            foreach (ApiDescription api in group.Items)
            {
                if (api.HttpMethod is null)
                {
                    continue;
                }

                // ApiDescription.RelativePath does not include the leading slash — add it.
                string relativePath = api.RelativePath ?? string.Empty;
                string pattern = relativePath.StartsWith('/') ? relativePath : "/" + relativePath;

                List<EndpointParameter> parameters = [];

                foreach (ApiParameterDescription param in api.ParameterDescriptions)
                {
                    parameters.Add(new EndpointParameter(
                        Name: param.Name,
                        ClrType: param.Type,
                        Source: param.Source,
                        IsRequired: param.IsRequired));
                }

                // Try to match by action descriptor's display name, then by route pattern.
                string? actionName = api.ActionDescriptor?.DisplayName;
                RouteEndpoint? matchedEndpoint = actionName is not null && endpointByName.TryGetValue(actionName, out RouteEndpoint? byName)
                    ? byName
                    : FindEndpointByPattern(dataSource, pattern, api.HttpMethod);

                EndpointMetadataCollection metadata = matchedEndpoint?.Metadata ?? new([]);
                bool requiresAuth = metadata.OfType<IAuthorizeData>().Any();

                // Build a synthetic RoutePattern for the relative path so callers get a
                // strongly-typed object with RawText populated.
                RoutePattern routePattern = RoutePatternFactory.Parse(pattern);

                results.Add(new DiscoveredEndpoint(
                    DisplayName: api.ActionDescriptor?.DisplayName ?? pattern,
                    HttpMethod: api.HttpMethod.ToUpperInvariant(),
                    RoutePattern: routePattern,
                    Parameters: parameters,
                    ProducesContentTypes: api.SupportedResponseTypes
                        .SelectMany(r => r.ApiResponseFormats.Select(f => f.MediaType))
                        .Distinct()
                        .ToList(),
                    ConsumesContentTypes: api.SupportedRequestFormats
                        .Select(f => f.MediaType)
                        .Distinct()
                        .ToList(),
                    RequiresAuthorization: requiresAuth,
                    Metadata: metadata));
            }
        }

        return results;
    }

    private static RouteEndpoint? FindEndpointByPattern(EndpointDataSource dataSource, string pattern, string httpMethod)
    {
        string normalizedPattern = NormalizePattern(pattern);
        return dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .FirstOrDefault(e =>
            {
                string epPattern = NormalizePattern(e.RoutePattern.RawText ?? string.Empty);
                if (!string.Equals(epPattern, normalizedPattern, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                IHttpMethodMetadata? methods = e.Metadata.GetMetadata<IHttpMethodMetadata>();
                return methods is not null &&
                       methods.HttpMethods.Any(m => string.Equals(m, httpMethod, StringComparison.OrdinalIgnoreCase));
            });
    }

    private static string NormalizePattern(string pattern)
    {
        // Remove leading slash.
        string normalized = pattern.TrimStart('/');
        // Strip inline constraints: {name:constraint} → {name}.
        normalized = ConstraintPattern.Replace(normalized, "{$1}");
        return normalized;
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

using Conjecture.Core;

namespace Conjecture.OpenApi;

/// <summary>An OpenAPI document that provides Conjecture strategies for request/response bodies and parameters.</summary>
public sealed class OpenApiDocument
{
    private readonly OpenApiSchemaAdapter adapter;

    internal Microsoft.OpenApi.Models.OpenApiDocument Raw { get; }

    internal OpenApiDocument(Microsoft.OpenApi.Models.OpenApiDocument raw)
    {
        Raw = raw;
        adapter = new OpenApiSchemaAdapter(raw);
    }

#pragma warning disable RS0016 // Symbol is not part of the declared public API
    /// <summary>Implicitly wraps a Microsoft OpenAPI document in a <see cref="OpenApiDocument"/>.</summary>
    public static implicit operator OpenApiDocument(global::Microsoft.OpenApi.Models.OpenApiDocument raw)
    {
        return new(raw);
    }
#pragma warning restore RS0016

    /// <summary>Returns a strategy that generates <see cref="JsonElement"/> values conforming to the request body schema for <paramref name="method"/> <paramref name="path"/>.</summary>
    public Strategy<JsonElement> RequestBody(string method, string path, int maxDepth = 5)
    {
        return adapter.RequestBody(method, path, maxDepth);
    }

    /// <summary>Returns a strategy that generates <see cref="JsonElement"/> values conforming to the response body schema for <paramref name="method"/> <paramref name="path"/> and <paramref name="statusCode"/>.</summary>
    public Strategy<JsonElement> ResponseBody(string method, string path, int statusCode, int maxDepth = 5)
    {
        return adapter.ResponseBody(method, path, statusCode, maxDepth);
    }

    /// <summary>Returns a strategy that generates <see cref="JsonElement"/> values conforming to the path parameter <paramref name="name"/> for <paramref name="method"/> <paramref name="path"/>.</summary>
    public Strategy<JsonElement> PathParameter(string method, string path, string name, int maxDepth = 5)
    {
        return adapter.PathParameter(method, path, name, maxDepth);
    }

    /// <summary>Returns a strategy that generates <see cref="JsonElement"/> values conforming to the query parameter <paramref name="name"/> for <paramref name="method"/> <paramref name="path"/>.</summary>
    public Strategy<JsonElement> QueryParameter(string method, string path, string name, int maxDepth = 5)
    {
        return adapter.QueryParameter(method, path, name, maxDepth);
    }
}
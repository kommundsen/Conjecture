// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Conjecture.Core;
using Conjecture.Http;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Conjecture.AspNetCore;

/// <summary>
/// Produces <see cref="Strategy{T}"/> of <see cref="HttpInteraction"/> for a single <see cref="DiscoveredEndpoint"/>.
/// </summary>
internal sealed class RequestSynthesizer(DiscoveredEndpoint endpoint, Conjecture.OpenApi.OpenApiDocument? openApiDoc = null)
{
    private readonly Conjecture.OpenApi.OpenApiDocument? openApiDoc = openApiDoc;

    private static readonly Dictionary<Type, Func<IGeneratorContext, object>> PrimitiveFactories = new()
    {
        [typeof(int)] = static ctx => ctx.Generate(Generate.Integers<int>()),
        [typeof(long)] = static ctx => ctx.Generate(Generate.Integers<long>()),
        [typeof(short)] = static ctx => ctx.Generate(Generate.Integers<short>()),
        [typeof(byte)] = static ctx => ctx.Generate(Generate.Integers<byte>()),
        [typeof(uint)] = static ctx => ctx.Generate(Generate.Integers<uint>()),
        [typeof(ulong)] = static ctx => ctx.Generate(Generate.Integers<ulong>()),
        [typeof(ushort)] = static ctx => ctx.Generate(Generate.Integers<ushort>()),
        [typeof(float)] = static ctx => ctx.Generate(Generate.Floats()),
        [typeof(double)] = static ctx => ctx.Generate(Generate.Doubles()),
        [typeof(decimal)] = static ctx => ctx.Generate(Generate.Decimals()),
        [typeof(bool)] = static ctx => ctx.Generate(Generate.Booleans()),
        [typeof(string)] = static ctx => ctx.Generate(Generate.Strings()),
        [typeof(Guid)] = static ctx => ctx.Generate(Generate.Guids()),
        [typeof(DateOnly)] = static ctx => ctx.Generate(Generate.DateOnlyValues()),
        [typeof(DateTime)] = static ctx => ctx.Generate(Generate.DateTimes()),
        [typeof(DateTimeOffset)] = static ctx => ctx.Generate(Generate.DateTimeOffsets()),
        [typeof(TimeSpan)] = static ctx => ctx.Generate(Generate.TimeSpans()),
        [typeof(TimeOnly)] = static ctx => ctx.Generate(Generate.TimeOnlyValues()),
    };

    /// <summary>
    /// Builds a <see cref="Strategy{T}"/> that generates well-formed <see cref="HttpInteraction"/> values.
    /// Throws <see cref="ArgumentException"/> at build time if any required parameter type is unknown.
    /// </summary>
    public Strategy<HttpInteraction> ValidStrategy()
    {
        ValidateBodyParameters();

        IReadOnlyList<EndpointParameter> captured = endpoint.Parameters;
        string method = endpoint.HttpMethod;
        string rawPattern = endpoint.RoutePattern.RawText ?? string.Empty;
        string contentType = endpoint.ConsumesContentTypes.Count > 0
            ? endpoint.ConsumesContentTypes[0]
            : "application/json";
        string accept = endpoint.ProducesContentTypes.Count > 0
            ? endpoint.ProducesContentTypes[0]
            : "application/json";

        Conjecture.OpenApi.OpenApiDocument? doc = this.openApiDoc;

        return Generate.Compose<HttpInteraction>(ctx =>
        {
            string path = BuildPath(rawPattern, captured, ctx);
            object? body = GenerateBody(captured, ctx, doc, method, rawPattern);
            IReadOnlyDictionary<string, string>? headers = BuildHeaders(contentType, accept, body);
            return new HttpInteraction(endpoint.DisplayName, method, path, body, headers);
        });
    }

    /// <summary>
    /// Builds a <see cref="Strategy{T}"/> that generates malformed <see cref="HttpInteraction"/> values.
    /// At least one structural aspect differs from a valid request.
    /// </summary>
    public Strategy<HttpInteraction> MalformedStrategy()
    {
        IReadOnlyList<EndpointParameter> captured = endpoint.Parameters;
        string method = endpoint.HttpMethod;
        string rawPattern = endpoint.RoutePattern.RawText ?? string.Empty;
        string displayName = endpoint.DisplayName;

        Strategy<HttpInteraction> wrongContentType = Generate.Compose<HttpInteraction>(ctx =>
        {
            string path = BuildPath(rawPattern, captured, ctx);
            IReadOnlyDictionary<string, string> headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "text/plain",
            };
            return new HttpInteraction(displayName, method, path, null, headers);
        });

        Strategy<HttpInteraction> nullBody = Generate.Compose<HttpInteraction>(ctx =>
        {
            string path = BuildPath(rawPattern, captured, ctx);
            IReadOnlyDictionary<string, string> headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
            };
            return new HttpInteraction(displayName, method, path, null, headers);
        });

        Strategy<HttpInteraction> malformedJson = Generate.Compose<HttpInteraction>(ctx =>
        {
            string path = BuildPath(rawPattern, captured, ctx);
            IReadOnlyDictionary<string, string> headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
            };
            return new HttpInteraction(displayName, method, path, "{ invalid json }", headers);
        });

        return Generate.OneOf(wrongContentType, nullBody, malformedJson);
    }

    private void ValidateBodyParameters()
    {
        foreach (EndpointParameter param in endpoint.Parameters)
        {
            if (param.Source != BindingSource.Body)
            {
                continue;
            }

            if (!IsSupportedType(param.ClrType))
            {
                throw new ArgumentException(
                    $"Cannot synthesize request for endpoint '{endpoint.DisplayName}': " +
                    $"parameter '{param.Name}' has type '{param.ClrType.FullName}' which is not registered with Generate.For<T>(). " +
                    $"Decorate the type with [Arbitrary] or register it manually via GenerateForRegistry.Register().",
                    param.Name);
            }
        }
    }

    private static bool IsSupportedType(Type type) =>
        PrimitiveFactories.ContainsKey(type) || GenerateForRegistry.IsRegistered(type);

    private static string BuildPath(string rawPattern, IReadOnlyList<EndpointParameter> parameters, IGeneratorContext ctx)
    {
        string path = rawPattern;

        IEnumerable<EndpointParameter> routeParams = parameters.Where(
            static p => p.Source == BindingSource.Path);

        foreach (EndpointParameter param in routeParams)
        {
            object value = GeneratePrimitive(param.ClrType, ctx);
            path = path.Replace("{" + param.Name + "}", value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        IEnumerable<EndpointParameter> queryParams = parameters
            .Where(static p => p.Source == BindingSource.Query && p.IsRequired)
            .ToList();

        if (queryParams.Any())
        {
            string queryString = string.Join(
                "&",
                queryParams.Select(p => $"{p.Name}={GeneratePrimitive(p.ClrType, ctx)}"));
            path = path + "?" + queryString;
        }

        return path;
    }

    private static object? GenerateBody(
        IReadOnlyList<EndpointParameter> parameters,
        IGeneratorContext ctx,
        Conjecture.OpenApi.OpenApiDocument? openApiDoc,
        string method,
        string path)
    {
        EndpointParameter? bodyParam = parameters.FirstOrDefault(
            static p => p.Source == BindingSource.Body);

        if (bodyParam is null)
        {
            return null;
        }

        if (openApiDoc is not null)
        {
            try
            {
                Strategy<JsonElement> docBody = openApiDoc.RequestBody(method, path);
                JsonElement element = ctx.Generate(docBody);
                return element.GetRawText();
            }
            catch (KeyNotFoundException)
            {
            }
        }

        if (PrimitiveFactories.ContainsKey(bodyParam.ClrType))
        {
            return GeneratePrimitive(bodyParam.ClrType, ctx);
        }

        // Use the registered boxed strategy.
        Strategy<object?> boxedStrategy = GenerateForRegistry.ResolveBoxed(bodyParam.ClrType);
        object? value = ctx.Generate(boxedStrategy);
        return JsonSerializer.Serialize(value);
    }

    private static IReadOnlyDictionary<string, string>? BuildHeaders(string contentType, string accept, object? body)
    {
        Dictionary<string, string> headers = [];

        if (body is not null)
        {
            headers["Content-Type"] = contentType;
        }

        headers["Accept"] = accept;

        return headers.Count > 0 ? headers : null;
    }

    private static object GeneratePrimitive(Type type, IGeneratorContext ctx) =>
        PrimitiveFactories.TryGetValue(type, out Func<IGeneratorContext, object>? factory)
            ? factory(ctx)
            : throw new ArgumentException(
                $"No built-in strategy for primitive type '{type.FullName}'.",
                nameof(type));
}
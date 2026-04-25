// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Text.Json;

using Conjecture.Core;
using Conjecture.JsonSchema;

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Conjecture.OpenApi;

internal sealed class OpenApiSchemaAdapter(OpenApiDocument document)
{
    internal Strategy<JsonElement> RequestBody(string method, string path, int maxDepth = 5)
    {
        OpenApiOperation operation = GetOperation(method, path);
        OpenApiRequestBody requestBody = operation.RequestBody
            ?? throw new KeyNotFoundException($"No request body for {method} {path}");
        OpenApiSchema schema = GetJsonSchema(requestBody.Content);
        JsonSchemaNode node = ConvertSchema(schema, document.Components?.Schemas);
        return new JsonSchemaStrategy(node, maxDepth);
    }

    internal Strategy<JsonElement> ResponseBody(string method, string path, int statusCode, int maxDepth = 5)
    {
        OpenApiOperation operation = GetOperation(method, path);
        string statusCodeStr = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Try exact match first, then fall back to any response with application/json content
        OpenApiResponse? response = null;
        if (!operation.Responses.TryGetValue(statusCodeStr, out response))
        {
            foreach (KeyValuePair<string, OpenApiResponse> kv in operation.Responses)
            {
                if (kv.Value.Content is not null && kv.Value.Content.ContainsKey("application/json"))
                {
                    response = kv.Value;
                    break;
                }
            }
        }

        if (response is null)
        {
            throw new KeyNotFoundException($"No response {statusCode} for {method} {path}");
        }

        if (response.Content is null || !response.Content.TryGetValue("application/json", out OpenApiMediaType? media))
        {
            throw new KeyNotFoundException($"No application/json content in response {statusCode} for {method} {path}");
        }

        OpenApiSchema schema = media.Schema
            ?? throw new KeyNotFoundException($"No schema in response {statusCode} for {method} {path}");
        JsonSchemaNode node = ConvertSchema(schema, document.Components?.Schemas);
        return new JsonSchemaStrategy(node, maxDepth);
    }

    internal Strategy<JsonElement> PathParameter(string method, string path, string parameterName, int maxDepth = 5)
    {
        return GetParameterStrategy(method, path, parameterName, ParameterLocation.Path, maxDepth);
    }

    internal Strategy<JsonElement> QueryParameter(string method, string path, string parameterName, int maxDepth = 5)
    {
        return GetParameterStrategy(method, path, parameterName, ParameterLocation.Query, maxDepth);
    }

    private Strategy<JsonElement> GetParameterStrategy(
        string method,
        string path,
        string parameterName,
        ParameterLocation location,
        int maxDepth)
    {
        OpenApiOperation operation = GetOperation(method, path);
        foreach (OpenApiParameter param in operation.Parameters)
        {
            if (param.Name == parameterName && param.In == location)
            {
                OpenApiSchema schema = param.Schema
                    ?? throw new KeyNotFoundException($"No schema for parameter {parameterName} in {method} {path}");
                JsonSchemaNode node = ConvertSchema(schema, document.Components?.Schemas);
                return new JsonSchemaStrategy(node, maxDepth);
            }
        }

        throw new KeyNotFoundException($"Parameter '{parameterName}' ({location}) not found in {method} {path}");
    }

    private OpenApiOperation GetOperation(string method, string path)
    {
        if (!document.Paths.TryGetValue(path, out OpenApiPathItem? pathItem))
        {
            throw new KeyNotFoundException($"Path '{path}' not found in document");
        }

        OperationType operationType = ParseOperationType(method);
        if (!pathItem.Operations.TryGetValue(operationType, out OpenApiOperation? operation))
        {
            throw new KeyNotFoundException($"Method '{method}' not found for path '{path}'");
        }

        return operation;
    }

    private static OperationType ParseOperationType(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => OperationType.Get,
            "POST" => OperationType.Post,
            "PUT" => OperationType.Put,
            "DELETE" => OperationType.Delete,
            "PATCH" => OperationType.Patch,
            "HEAD" => OperationType.Head,
            "OPTIONS" => OperationType.Options,
            "TRACE" => OperationType.Trace,
            _ => throw new KeyNotFoundException($"Unknown HTTP method '{method}'"),
        };
    }

    private static OpenApiSchema GetJsonSchema(IDictionary<string, OpenApiMediaType>? content)
    {
        if (content is null || !content.TryGetValue("application/json", out OpenApiMediaType? media))
        {
            throw new KeyNotFoundException("No application/json content");
        }

        return media.Schema ?? throw new KeyNotFoundException("No schema in application/json content");
    }

    private static JsonSchemaNode ConvertSchema(
        OpenApiSchema schema,
        IDictionary<string, OpenApiSchema>? components)
    {
        // When OpenApi reader resolves a $ref, the schema has Reference set but properties are inlined.
        // We still need to handle the case where the schema only has a Reference and no inline properties.
        if (schema.Reference is not null && schema.Properties.Count == 0 && schema.Type is null
            && schema.Items is null && schema.OneOf.Count == 0 && schema.AnyOf.Count == 0
            && schema.AllOf.Count == 0 && schema.Enum.Count == 0)
        {
            // Unresolved ref - look it up in components
            string refId = schema.Reference.Id;
            if (components is not null && components.TryGetValue(refId, out OpenApiSchema? resolved))
            {
                return ConvertSchema(resolved, components);
            }
        }

        JsonSchemaType type = ConvertType(schema.Type);

        IReadOnlyDictionary<string, JsonSchemaNode>? properties = ConvertProperties(schema.Properties, components);
        IReadOnlyList<string>? required = schema.Required.Count > 0 ? [.. schema.Required] : null;
        JsonSchemaNode? items = schema.Items is not null ? ConvertSchema(schema.Items, components) : null;

        IReadOnlyList<JsonSchemaNode>? oneOf = ConvertSchemaList(schema.OneOf, components);
        IReadOnlyList<JsonSchemaNode>? anyOf = ConvertSchemaList(schema.AnyOf, components);
        IReadOnlyList<JsonSchemaNode>? allOf = ConvertSchemaList(schema.AllOf, components);

        long? minimum = schema.Minimum.HasValue ? (long)schema.Minimum.Value : null;
        long? maximum = schema.Maximum.HasValue ? (long)schema.Maximum.Value : null;
        double? minimumDouble = schema.Minimum.HasValue ? (double)schema.Minimum.Value : null;
        double? maximumDouble = schema.Maximum.HasValue ? (double)schema.Maximum.Value : null;

        bool exclusiveMinimum = schema.ExclusiveMinimum ?? false;
        bool exclusiveMaximum = schema.ExclusiveMaximum ?? false;

        int? minLength = schema.MinLength;
        int? maxLength = schema.MaxLength;
        string? pattern = schema.Pattern;
        string? format = schema.Format;
        int? minItems = schema.MinItems;
        int? maxItems = schema.MaxItems;

        IReadOnlyList<JsonElement>? enumValues = ConvertEnum(schema.Enum);

        return new(
            type,
            oneOf,
            anyOf,
            allOf,
            properties,
            required,
            items,
            minItems,
            maxItems,
            minimum,
            maximum,
            minimumDouble,
            maximumDouble,
            exclusiveMinimum,
            exclusiveMaximum,
            minLength,
            maxLength,
            pattern,
            enumValues,
            null,
            null,
            format);
    }

    private static JsonSchemaType ConvertType(string? type)
    {
        return type switch
        {
            "boolean" => JsonSchemaType.Boolean,
            "integer" => JsonSchemaType.Integer,
            "number" => JsonSchemaType.Number,
            "string" => JsonSchemaType.String,
            "array" => JsonSchemaType.Array,
            "object" => JsonSchemaType.Object,
            "null" => JsonSchemaType.Null,
            null => JsonSchemaType.None,
            _ => JsonSchemaType.None,
        };
    }

    private static IReadOnlyDictionary<string, JsonSchemaNode>? ConvertProperties(
        IDictionary<string, OpenApiSchema>? properties,
        IDictionary<string, OpenApiSchema>? components)
    {
        if (properties is null || properties.Count == 0)
        {
            return null;
        }

        Dictionary<string, JsonSchemaNode> result = new(properties.Count);
        foreach (KeyValuePair<string, OpenApiSchema> kv in properties)
        {
            result[kv.Key] = ConvertSchema(kv.Value, components);
        }

        return result;
    }

    private static IReadOnlyList<JsonSchemaNode>? ConvertSchemaList(
        IList<OpenApiSchema>? list,
        IDictionary<string, OpenApiSchema>? components)
    {
        if (list is null || list.Count == 0)
        {
            return null;
        }

        JsonSchemaNode[] result = new JsonSchemaNode[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            result[i] = ConvertSchema(list[i], components);
        }

        return result;
    }

    private static IReadOnlyList<JsonElement>? ConvertEnum(IList<IOpenApiAny>? enumValues)
    {
        if (enumValues is null || enumValues.Count == 0)
        {
            return null;
        }

        List<JsonElement> result = new(enumValues.Count);
        foreach (IOpenApiAny value in enumValues)
        {
            JsonElement? element = ConvertAnyToJsonElement(value);
            if (element.HasValue)
            {
                result.Add(element.Value);
            }
        }

        return result.Count > 0 ? result : null;
    }

    private static JsonElement? ConvertAnyToJsonElement(IOpenApiAny value)
    {
        if (value is OpenApiPrimitive<string> strVal)
        {
            using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(strVal.Value));
            return doc.RootElement.Clone();
        }

        if (value is OpenApiPrimitive<int> intVal)
        {
            using JsonDocument doc = JsonDocument.Parse(intVal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return doc.RootElement.Clone();
        }

        if (value is OpenApiPrimitive<long> longVal)
        {
            using JsonDocument doc = JsonDocument.Parse(longVal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return doc.RootElement.Clone();
        }

        if (value is OpenApiPrimitive<double> dblVal)
        {
            using JsonDocument doc = JsonDocument.Parse(dblVal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return doc.RootElement.Clone();
        }

        if (value is OpenApiPrimitive<bool> boolVal)
        {
            using JsonDocument doc = JsonDocument.Parse(boolVal.Value ? "true" : "false");
            return doc.RootElement.Clone();
        }

        return null;
    }
}

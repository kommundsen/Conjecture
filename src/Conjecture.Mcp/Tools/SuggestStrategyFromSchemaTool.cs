// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.ComponentModel;

using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tools;

[McpServerToolType]
internal static class SuggestStrategyFromSchemaTool
{
    [McpServerTool(Name = "suggest-strategy-from-schema")]
    [Description("Generates a suggested [Property] test scaffold from a schema file or URL")]
    public static string SuggestStrategyFromSchema(
        [Description("The schema type: 'openapi', 'json-schema', or 'protobuf'")] string schemaType,
        [Description("File path or URL (optional for protobuf)")] string? schemaPath = null,
        [Description("Endpoint, e.g. 'POST /api/orders' (required for openapi)")] string? endpoint = null,
        [Description("C# type name (required for protobuf)")] string? messageType = null)
    {
        return schemaType switch
        {
            "openapi" => SuggestOpenApi(schemaPath, endpoint),
            "json-schema" => SuggestJsonSchema(schemaPath),
            "protobuf" => SuggestProtobuf(messageType),
            _ => $"error: unknown schemaType '{schemaType}'. Expected 'openapi', 'json-schema', or 'protobuf'."
        };
    }

    private static string SuggestOpenApi(string? schemaPath, string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "error: 'endpoint' is required for schemaType 'openapi'. Example: \"POST /api/orders\"";
        }

        string path = string.IsNullOrWhiteSpace(schemaPath) ? "swagger.json" : schemaPath;
        string methodName = EndpointToMethodName(endpoint);

        return $$"""
            // In test setup:
            private static readonly Task<OpenApiDocument> Schema =
                Strategy.FromOpenApi("{{path}}");

            [Property]
            public async Task<bool> {{methodName}}_IsIdempotent(JsonElement request)
            {
                var doc = await Schema;
                // TODO: replace with your HTTP client call
                return true;
            }
            """;
    }

    private static string SuggestJsonSchema(string? schemaPath)
    {
        string path = string.IsNullOrWhiteSpace(schemaPath) ? "schema.json" : schemaPath;

        return $$"""
            // In test setup:
            private static readonly Task<JsonSchema> Schema =
                Strategy.FromJsonSchema("{{path}}");

            [Property]
            public async Task<bool> Schema_IsValid(JsonElement request)
            {
                JsonSchema schema = await Schema;
                // TODO: replace with your validation logic
                return true;
            }
            """;
    }

    private static string SuggestProtobuf(string? messageType)
    {
        return string.IsNullOrWhiteSpace(messageType)
            ? "error: 'messageType' is required for schemaType 'protobuf'. Example: \"MyMessage\""
            : $$"""
            [Property]
            public bool {{messageType}}_RoundTripsCorrectly({{messageType}} message)
            {
                Strategy<{{messageType}}> strategy = Strategy.FromProtobuf<{{messageType}}>();
                // TODO: replace with your serialization roundtrip assertion
                return true;
            }
            """;
    }

    private static string EndpointToMethodName(string endpoint)
    {
        return endpoint
            .Replace(" ", "_")
            .Replace("/", "_")
            .Replace("{", "")
            .Replace("}", "")
            .Trim('_');
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Mcp.Tools;

namespace Conjecture.Mcp.Tests.Tools;

public class SuggestStrategyFromSchemaToolTests
{
    [Fact]
    public void SuggestStrategyFromSchema_OpenApi_ReturnsNonEmptyCode()
    {
        string result = SuggestStrategyFromSchemaTool.SuggestStrategyFromSchema(
            schemaType: "openapi",
            schemaPath: "swagger.json",
            endpoint: "POST /api/orders");

        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void SuggestStrategyFromSchema_OpenApi_ContainsGenerateFromOpenApi()
    {
        string result = SuggestStrategyFromSchemaTool.SuggestStrategyFromSchema(
            schemaType: "openapi",
            schemaPath: "swagger.json",
            endpoint: "POST /api/orders");

        Assert.Contains("Strategy.FromOpenApi", result);
    }

    [Fact]
    public void SuggestStrategyFromSchema_JsonSchema_ContainsGenerateFromJsonSchema()
    {
        string result = SuggestStrategyFromSchemaTool.SuggestStrategyFromSchema(
            schemaType: "json-schema",
            schemaPath: "schema.json");

        Assert.Contains("Strategy.FromJsonSchema", result);
    }

    [Fact]
    public void SuggestStrategyFromSchema_Protobuf_ContainsGenerateFromProtobuf()
    {
        string result = SuggestStrategyFromSchemaTool.SuggestStrategyFromSchema(
            schemaType: "protobuf",
            messageType: "MyMessage");

        Assert.Contains("Strategy.FromProtobuf<MyMessage>", result);
    }

    [Fact]
    public void SuggestStrategyFromSchema_OpenApiMissingEndpoint_ReturnsDescriptiveError()
    {
        string result = SuggestStrategyFromSchemaTool.SuggestStrategyFromSchema(
            schemaType: "openapi",
            schemaPath: "swagger.json");

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.True(
            result.Contains("endpoint") || result.Contains("required") || result.Contains("error"),
            $"Expected error mentioning missing endpoint, got: {result}");
    }
}
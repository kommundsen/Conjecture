// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text.Json;

using Conjecture.Abstractions.JsonSchema;
using Conjecture.JsonSchema;

namespace Conjecture.JsonSchema.Tests;

public sealed class JsonSchemaParserTests
{
    [Fact]
    public void Parse_StringType_ReturnsNodeWithTypeString()
    {
        using JsonDocument doc = JsonDocument.Parse("""{"type": "string"}""");
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        Assert.Equal(JsonSchemaType.String, node.Type);
    }

    [Fact]
    public void Parse_IntegerTypeWithBounds_ReturnsNodeWithMinimumAndMaximum()
    {
        using JsonDocument doc = JsonDocument.Parse("""{"type": "integer", "minimum": 0, "maximum": 100}""");
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        Assert.Equal(JsonSchemaType.Integer, node.Type);
        Assert.Equal(0L, node.Minimum);
        Assert.Equal(100L, node.Maximum);
    }

    [Fact]
    public void Parse_ObjectWithProperties_ReturnsNodeWithPropertiesPopulated()
    {
        using JsonDocument doc = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "name": {"type": "string"},
                "age":  {"type": "integer"}
              },
              "required": ["name"]
            }
            """);
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        Assert.Equal(JsonSchemaType.Object, node.Type);
        Assert.NotNull(node.Properties);
        Assert.True(node.Properties!.ContainsKey("name"));
        Assert.True(node.Properties!.ContainsKey("age"));
        Assert.Equal(JsonSchemaType.String, node.Properties!["name"].Type);
        Assert.Equal(JsonSchemaType.Integer, node.Properties!["age"].Type);
    }

    [Fact]
    public void Parse_ObjectWithRequired_ReturnsNodeWithRequiredList()
    {
        using JsonDocument doc = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {"name": {"type": "string"}},
              "required": ["name"]
            }
            """);
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        Assert.NotNull(node.Required);
        Assert.Contains("name", node.Required!);
    }

    [Fact]
    public void Resolve_RefToDefsEntry_ReturnsNodeWithRefResolved()
    {
        using JsonDocument doc = JsonDocument.Parse("""
            {
              "$ref": "#/$defs/Foo",
              "$defs": {
                "Foo": {"type": "string"}
              }
            }
            """);
        JsonSchemaNode parsed = JsonSchemaParser.Parse(doc.RootElement);
        JsonSchemaNode resolved = JsonSchemaRefResolver.Resolve(parsed);
        Assert.Null(resolved.Ref);
        Assert.Equal(JsonSchemaType.String, resolved.Type);
    }

    [Fact]
    public void Resolve_CyclicRef_DoesNotLoopInfinitely()
    {
        using JsonDocument doc = JsonDocument.Parse("""
            {
              "$defs": {
                "Node": {
                  "type": "object",
                  "properties": {
                    "child": {"$ref": "#/$defs/Node"}
                  }
                }
              },
              "$ref": "#/$defs/Node"
            }
            """);
        JsonSchemaNode parsed = JsonSchemaParser.Parse(doc.RootElement);
        // Must not throw StackOverflowException or loop; just return a node
        JsonSchemaNode resolved = JsonSchemaRefResolver.Resolve(parsed);
        Assert.Equal(JsonSchemaType.Object, resolved.Type);
    }

    [Fact]
    public void Parse_OneOfArray_ReturnsNodeWithOneOfPopulated()
    {
        using JsonDocument doc = JsonDocument.Parse("""
            {
              "oneOf": [
                {"type": "string"},
                {"type": "integer"}
              ]
            }
            """);
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        Assert.NotNull(node.OneOf);
        Assert.Equal(2, node.OneOf!.Count);
        Assert.Equal(JsonSchemaType.String, node.OneOf![0].Type);
        Assert.Equal(JsonSchemaType.Integer, node.OneOf![1].Type);
    }

    [Fact]
    public void Parse_AnyOfArray_ReturnsNodeWithAnyOfPopulated()
    {
        using JsonDocument doc = JsonDocument.Parse("""
            {
              "anyOf": [
                {"type": "boolean"},
                {"type": "null"}
              ]
            }
            """);
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        Assert.NotNull(node.AnyOf);
        Assert.Equal(2, node.AnyOf!.Count);
    }

    [Fact]
    public void Parse_AllOfArray_ReturnsNodeWithAllOfPopulated()
    {
        using JsonDocument doc = JsonDocument.Parse("""
            {
              "allOf": [
                {"type": "object"},
                {"properties": {"x": {"type": "number"}}}
              ]
            }
            """);
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        Assert.NotNull(node.AllOf);
        Assert.Equal(2, node.AllOf!.Count);
    }

    [Fact]
    public void Parse_ArrayWithItems_ReturnsNodeWithItemsPopulated()
    {
        using JsonDocument doc = JsonDocument.Parse("""
            {
              "type": "array",
              "items": {"type": "string"},
              "minItems": 1,
              "maxItems": 10
            }
            """);
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        Assert.Equal(JsonSchemaType.Array, node.Type);
        Assert.NotNull(node.Items);
        Assert.Equal(JsonSchemaType.String, node.Items!.Type);
        Assert.Equal(1, node.MinItems);
        Assert.Equal(10, node.MaxItems);
    }

    [Fact]
    public void Parse_UnknownKeywords_DoesNotThrow()
    {
        using JsonDocument doc = JsonDocument.Parse("""
            {
              "type": "string",
              "$schema": "http://json-schema.org/draft-07/schema#",
              "title": "MySchema",
              "description": "A test schema",
              "x-custom": true
            }
            """);
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        Assert.Equal(JsonSchemaType.String, node.Type);
    }

    [Theory]
    [InlineData("null", JsonSchemaType.Null)]
    [InlineData("boolean", JsonSchemaType.Boolean)]
    [InlineData("number", JsonSchemaType.Number)]
    [InlineData("array", JsonSchemaType.Array)]
    [InlineData("object", JsonSchemaType.Object)]
    public void Parse_KnownTypeKeyword_ReturnsCorrectType(string typeValue, JsonSchemaType expectedType)
    {
        using JsonDocument doc = JsonDocument.Parse($$"""{"type": "{{typeValue}}"}""");
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        Assert.Equal(expectedType, node.Type);
    }
}
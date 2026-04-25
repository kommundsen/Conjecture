// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

using Conjecture.Core;
using Conjecture.JsonSchema;
using Conjecture.Regex;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.JsonSchema.Tests;

public sealed class JsonSchemaStrategyTests
{
    private static JsonSchemaStrategy ParseStrategy(string schemaJson, int maxDepth = 5)
    {
        using JsonDocument doc = JsonDocument.Parse(schemaJson);
        JsonSchemaNode node = JsonSchemaParser.Parse(doc.RootElement);
        return new(node, maxDepth);
    }

    private static IReadOnlyList<JsonElement> Sample(JsonSchemaStrategy strategy, int count = 50, ulong seed = 42UL)
    {
        return DataGen.Sample(strategy, count, seed);
    }

    [Fact]
    public void Generate_BooleanSchema_ProducesOnlyBooleans()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "boolean"}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.True(
                element.ValueKind is JsonValueKind.True or JsonValueKind.False,
                $"Expected boolean but got {element.ValueKind}");
        }
    }

    [Fact]
    public void Generate_IntegerSchemaWithBounds_ProducesValuesWithinRange()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "integer", "minimum": 10, "maximum": 20}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Number, element.ValueKind);
            long value = element.GetInt64();
            Assert.InRange(value, 10L, 20L);
        }
    }

    [Fact]
    public void Generate_StringSchemaWithLengthBounds_ProducesStringsWithinLengthRange()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "string", "minLength": 3, "maxLength": 8}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            string value = element.GetString()!;
            Assert.InRange(value.Length, 3, 8);
        }
    }

    [Fact]
    public void Generate_StringSchemaWithPattern_ProducesMatchingStrings()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "string", "pattern": "^[0-9]{4}$"}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        DotNetRegex pattern = new(@"^[0-9]{4}$");
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.True(pattern.IsMatch(element.GetString()!), $"Value '{element.GetString()}' did not match pattern");
        }
    }

    [Fact]
    public void Generate_StringSchemaWithFormatEmail_ProducesEmailAddresses()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "string", "format": "email"}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.True(KnownRegex.Email.IsMatch(element.GetString()!), $"Value '{element.GetString()}' is not an email");
        }
    }

    [Fact]
    public void Generate_StringSchemaWithFormatUri_ProducesUrls()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "string", "format": "uri"}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.True(KnownRegex.Url.IsMatch(element.GetString()!), $"Value '{element.GetString()}' is not a URL");
        }
    }

    [Fact]
    public void Generate_StringSchemaWithFormatUuid_ProducesUuids()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "string", "format": "uuid"}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.True(KnownRegex.Uuid.IsMatch(element.GetString()!), $"Value '{element.GetString()}' is not a UUID");
        }
    }

    [Fact]
    public void Generate_StringSchemaWithFormatDateTime_ProducesIsoDateStrings()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "string", "format": "date-time"}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.True(KnownRegex.IsoDate.IsMatch(element.GetString()!), $"Value '{element.GetString()}' is not an ISO date");
        }
    }

    [Fact]
    public void Generate_EnumSchema_ProducesOnlyEnumValues()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"enum": ["red", "green", "blue"]}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        HashSet<string> allowed = ["red", "green", "blue"];
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.Contains(element.GetString()!, allowed);
        }
    }

    [Fact]
    public void Generate_ConstSchema_AlwaysProducesConstValue()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"const": 42}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Number, element.ValueKind);
            Assert.Equal(42L, element.GetInt64());
        }
    }

    [Fact]
    public void Generate_ObjectSchemaWithRequired_AlwaysIncludesRequiredFields()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""
            {
              "type": "object",
              "properties": {
                "id": {"type": "integer", "minimum": 1, "maximum": 100},
                "name": {"type": "string", "minLength": 1, "maxLength": 10},
                "optional": {"type": "string"}
              },
              "required": ["id", "name"]
            }
            """);
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
            Assert.True(element.TryGetProperty("id", out _), "Required field 'id' was absent");
            Assert.True(element.TryGetProperty("name", out _), "Required field 'name' was absent");
        }
    }

    [Fact]
    public void Generate_ObjectSchemaWithOptional_SometimesOmitsOptionalFields()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""
            {
              "type": "object",
              "properties": {
                "required_field": {"type": "integer", "minimum": 0, "maximum": 10},
                "optional_field": {"type": "string", "minLength": 1, "maxLength": 5}
              },
              "required": ["required_field"]
            }
            """);
        IReadOnlyList<JsonElement> samples = Sample(strategy, count: 100);
        bool anyAbsent = samples.Any(e =>
            e.ValueKind == JsonValueKind.Object && !e.TryGetProperty("optional_field", out _));
        bool anyPresent = samples.Any(e =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty("optional_field", out _));
        Assert.True(anyAbsent, "Optional field was always present — should sometimes be absent");
        Assert.True(anyPresent, "Optional field was never present — should sometimes be present");
    }

    [Fact]
    public void Generate_ArraySchema_ProducesArraysWithinItemBounds()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""
            {
              "type": "array",
              "items": {"type": "integer", "minimum": 0, "maximum": 100},
              "minItems": 2,
              "maxItems": 5
            }
            """);
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.Array, element.ValueKind);
            int length = element.GetArrayLength();
            Assert.InRange(length, 2, 5);
            foreach (JsonElement item in element.EnumerateArray())
            {
                Assert.Equal(JsonValueKind.Number, item.ValueKind);
                Assert.InRange(item.GetInt64(), 0L, 100L);
            }
        }
    }

    [Fact]
    public void Generate_RecursiveRefSchema_DoesNotStackOverflow()
    {
        JsonSchemaStrategy strategy = ParseStrategy(
            """
            {
              "$defs": {
                "Node": {
                  "type": "object",
                  "properties": {
                    "value": {"type": "integer", "minimum": 0, "maximum": 10},
                    "child": {"$ref": "#/$defs/Node"}
                  },
                  "required": ["value"]
                }
              },
              "$ref": "#/$defs/Node"
            }
            """,
            maxDepth: 3);
        Exception? exception = Record.Exception(() => Sample(strategy, count: 10));
        Assert.Null(exception);
    }

    [Fact]
    public void Generate_IntegerSchema_ShrinksToBoundaryMinimum()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "integer", "minimum": 5, "maximum": 100}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy, count: 200);
        bool foundMinimum = samples.Any(e => e.ValueKind == JsonValueKind.Number && e.GetInt64() == 5L);
        Assert.True(foundMinimum, "Expected minimum boundary value 5 to appear in samples");
    }

    [Fact]
    public void Generate_IntegerSchema_NeverProducesValueBelowMinimum()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "integer", "minimum": 5, "maximum": 100}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy, count: 200);
        foreach (JsonElement element in samples)
        {
            Assert.True(element.GetInt64() >= 5L, $"Value {element.GetInt64()} is below minimum 5");
        }
    }

    [Fact]
    public void Generate_StringSchemaWithFormatIpv4_ProducesIpv4Addresses()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "string", "format": "ipv4"}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.Matches(KnownRegex.Ipv4, element.GetString()!);
        }
    }

    [Fact]
    public void Generate_StringSchemaWithFormatIpv6_ProducesIpv6Addresses()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "string", "format": "ipv6"}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.Matches(KnownRegex.Ipv6, element.GetString()!);
        }
    }

    [Fact]
    public void Generate_StringSchemaWithFormatDate_ProducesRfc3339DateStrings()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "string", "format": "date"}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.Matches(KnownRegex.Date, element.GetString()!);
        }
    }

    [Fact]
    public void Generate_StringSchemaWithFormatTime_ProducesRfc3339TimeStrings()
    {
        JsonSchemaStrategy strategy = ParseStrategy("""{"type": "string", "format": "time"}""");
        IReadOnlyList<JsonElement> samples = Sample(strategy);
        foreach (JsonElement element in samples)
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
            Assert.Matches(KnownRegex.Time, element.GetString()!);
        }
    }
}

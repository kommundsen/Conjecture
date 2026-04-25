// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text.Json;

namespace Conjecture.JsonSchema;

internal static class JsonSchemaParser
{
    internal static JsonSchemaNode Parse(JsonElement root)
    {
        Dictionary<string, JsonSchemaNode>? defs = null;
        if (root.TryGetProperty("$defs", out JsonElement defsElement))
        {
            defs = ParseProperties(defsElement, null);
        }
        else if (root.TryGetProperty("definitions", out JsonElement definitionsElement))
        {
            defs = ParseProperties(definitionsElement, null);
        }

        return ParseElement(root, defs);
    }

    internal static JsonSchemaNode Resolve(JsonSchemaNode root)
    {
        HashSet<string> visiting = [];
        return ResolveNode(root, root.Defs, visiting);
    }

    private static JsonSchemaNode ResolveNode(
        JsonSchemaNode node,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs,
        HashSet<string> visiting)
    {
        if (node.Ref is not null)
        {
            string refValue = node.Ref;
            if (visiting.Contains(refValue))
            {
                return node with { Ref = null };
            }

            visiting.Add(refValue);
            JsonSchemaNode? resolved = JsonSchemaRefResolver.ResolveRef(refValue, defs);
            if (resolved is not null)
            {
                JsonSchemaNode result = ResolveNode(resolved, defs ?? resolved.Defs, visiting);
                visiting.Remove(refValue);
                return result;
            }

            visiting.Remove(refValue);
        }

        return node with
        {
            OneOf = ResolveList(node.OneOf, defs, visiting),
            AnyOf = ResolveList(node.AnyOf, defs, visiting),
            AllOf = ResolveList(node.AllOf, defs, visiting),
            Properties = ResolveDict(node.Properties, defs, visiting),
            Items = node.Items is null ? null : ResolveNode(node.Items, defs, visiting),
        };
    }

    private static IReadOnlyList<JsonSchemaNode>? ResolveList(
        IReadOnlyList<JsonSchemaNode>? list,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs,
        HashSet<string> visiting)
    {
        if (list is null)
        {
            return null;
        }

        List<JsonSchemaNode> result = new(list.Count);
        foreach (JsonSchemaNode item in list)
        {
            result.Add(ResolveNode(item, defs, visiting));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, JsonSchemaNode>? ResolveDict(
        IReadOnlyDictionary<string, JsonSchemaNode>? dict,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs,
        HashSet<string> visiting)
    {
        if (dict is null)
        {
            return null;
        }

        Dictionary<string, JsonSchemaNode> result = new(dict.Count);
        foreach (KeyValuePair<string, JsonSchemaNode> kv in dict)
        {
            result[kv.Key] = ResolveNode(kv.Value, defs, visiting);
        }

        return result;
    }

    private static JsonSchemaNode ParseElement(JsonElement element, IReadOnlyDictionary<string, JsonSchemaNode>? defs)
    {
        JsonSchemaType type = JsonSchemaType.None;
        IReadOnlyList<JsonSchemaNode>? oneOf = null;
        IReadOnlyList<JsonSchemaNode>? anyOf = null;
        IReadOnlyList<JsonSchemaNode>? allOf = null;
        IReadOnlyDictionary<string, JsonSchemaNode>? properties = null;
        IReadOnlyList<string>? required = null;
        JsonSchemaNode? items = null;
        int? minItems = null;
        int? maxItems = null;
        long? minimum = null;
        long? maximum = null;
        double? minimumDouble = null;
        double? maximumDouble = null;
        bool exclusiveMinimum = false;
        bool exclusiveMaximum = false;
        int? minLength = null;
        int? maxLength = null;
        string? pattern = null;
        IReadOnlyList<JsonElement>? enumValues = null;
        JsonElement? constValue = null;
        string? refValue = null;
        string? format = null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return new(
                type, oneOf, anyOf, allOf, properties, required, items,
                minItems, maxItems, minimum, maximum, minimumDouble, maximumDouble,
                exclusiveMinimum, exclusiveMaximum,
                minLength, maxLength, pattern, enumValues, constValue, refValue, format)
            {
                Defs = defs,
            };
        }

        foreach (JsonProperty prop in element.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "type":
                    type = ParseType(prop.Value);
                    break;
                case "oneOf":
                    oneOf = ParseSchemaArray(prop.Value, defs);
                    break;
                case "anyOf":
                    anyOf = ParseSchemaArray(prop.Value, defs);
                    break;
                case "allOf":
                    allOf = ParseSchemaArray(prop.Value, defs);
                    break;
                case "properties":
                    properties = ParseProperties(prop.Value, defs);
                    break;
                case "required":
                    required = ParseStringArray(prop.Value);
                    break;
                case "items":
                    items = ParseElement(prop.Value, defs);
                    break;
                case "minItems":
                    minItems = prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetInt32() : null;
                    break;
                case "maxItems":
                    maxItems = prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetInt32() : null;
                    break;
                case "minimum":
                    if (prop.Value.ValueKind == JsonValueKind.Number)
                    {
                        minimum = prop.Value.TryGetInt64(out long minLong) ? minLong : (long?)null;
                        minimumDouble = prop.Value.GetDouble();
                    }

                    break;
                case "maximum":
                    if (prop.Value.ValueKind == JsonValueKind.Number)
                    {
                        maximum = prop.Value.TryGetInt64(out long maxLong) ? maxLong : (long?)null;
                        maximumDouble = prop.Value.GetDouble();
                    }

                    break;
                case "exclusiveMinimum":
                    if (prop.Value.ValueKind == JsonValueKind.Number)
                    {
                        minimum = prop.Value.TryGetInt64(out long exMinLong) ? exMinLong : (long?)null;
                        minimumDouble = prop.Value.GetDouble();
                        exclusiveMinimum = true;
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.True)
                    {
                        exclusiveMinimum = true;
                    }

                    break;
                case "exclusiveMaximum":
                    if (prop.Value.ValueKind == JsonValueKind.Number)
                    {
                        maximum = prop.Value.TryGetInt64(out long exMaxLong) ? exMaxLong : (long?)null;
                        maximumDouble = prop.Value.GetDouble();
                        exclusiveMaximum = true;
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.True)
                    {
                        exclusiveMaximum = true;
                    }

                    break;
                case "minLength":
                    minLength = prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetInt32() : null;
                    break;
                case "maxLength":
                    maxLength = prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetInt32() : null;
                    break;
                case "pattern":
                    pattern = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                    break;
                case "enum":
                    enumValues = ParseEnumArray(prop.Value);
                    break;
                case "const":
                    constValue = prop.Value.Clone();
                    break;
                case "$ref":
                    refValue = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                    break;
                case "format":
                    format = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                    break;
                default:
                    break;
            }
        }

        return new(
            type, oneOf, anyOf, allOf, properties, required, items,
            minItems, maxItems, minimum, maximum, minimumDouble, maximumDouble,
            exclusiveMinimum, exclusiveMaximum,
            minLength, maxLength, pattern, enumValues, constValue, refValue, format)
        {
            Defs = defs,
        };
    }

    private static JsonSchemaType ParseType(JsonElement element)
    {
        return element.ValueKind != JsonValueKind.String
            ? JsonSchemaType.None
            : element.GetString() switch
            {
                "null" => JsonSchemaType.Null,
                "boolean" => JsonSchemaType.Boolean,
                "integer" => JsonSchemaType.Integer,
                "number" => JsonSchemaType.Number,
                "string" => JsonSchemaType.String,
                "array" => JsonSchemaType.Array,
                "object" => JsonSchemaType.Object,
                _ => JsonSchemaType.None,
            };
    }

    private static List<JsonSchemaNode> ParseSchemaArray(JsonElement element, IReadOnlyDictionary<string, JsonSchemaNode>? defs)
    {
        List<JsonSchemaNode> result = [];
        if (element.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (JsonElement item in element.EnumerateArray())
        {
            result.Add(ParseElement(item, defs));
        }

        return result;
    }

    private static Dictionary<string, JsonSchemaNode> ParseProperties(JsonElement element, IReadOnlyDictionary<string, JsonSchemaNode>? defs)
    {
        Dictionary<string, JsonSchemaNode> result = [];
        if (element.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (JsonProperty prop in element.EnumerateObject())
        {
            result[prop.Name] = ParseElement(prop.Value, defs);
        }

        return result;
    }

    private static List<string> ParseStringArray(JsonElement element)
    {
        List<string> result = [];
        if (element.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? value = item.GetString();
                if (value is not null)
                {
                    result.Add(value);
                }
            }
        }

        return result;
    }

    private static List<JsonElement> ParseEnumArray(JsonElement element)
    {
        List<JsonElement> result = [];
        if (element.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (JsonElement item in element.EnumerateArray())
        {
            result.Add(item.Clone());
        }

        return result;
    }
}
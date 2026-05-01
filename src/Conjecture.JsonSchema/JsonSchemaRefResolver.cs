// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text.Json;

using Conjecture.Abstractions.JsonSchema;

namespace Conjecture.JsonSchema;

internal static class JsonSchemaRefResolver
{
    internal static JsonSchemaNode? ResolveRef(string refValue, IReadOnlyDictionary<string, JsonSchemaNode>? defs)
    {
        if (defs is null)
        {
            return null;
        }

        // Handle #/$defs/Name and #/definitions/Name
        const string defsPrefix = "#/$defs/";
        const string definitionsPrefix = "#/definitions/";

        string? name = null;
        if (refValue.StartsWith(defsPrefix, System.StringComparison.Ordinal))
        {
            name = refValue[defsPrefix.Length..];
        }
        else if (refValue.StartsWith(definitionsPrefix, System.StringComparison.Ordinal))
        {
            name = refValue[definitionsPrefix.Length..];
        }

        return name is null ? null : defs.TryGetValue(name, out JsonSchemaNode? found) ? found : null;
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
            JsonSchemaNode? resolved = ResolveRef(refValue, defs);
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

    internal static readonly JsonElement TrueElement;
    internal static readonly JsonElement FalseElement;
    internal static readonly JsonElement NullElement;

    static JsonSchemaRefResolver()
    {
        TrueElement = JsonDocument.Parse("true").RootElement.Clone();
        FalseElement = JsonDocument.Parse("false").RootElement.Clone();
        NullElement = JsonDocument.Parse("null").RootElement.Clone();
    }
}
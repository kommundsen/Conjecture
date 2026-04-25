// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text.Json;

using Conjecture.Core;
using Conjecture.Core.Internal;

using Gen = Conjecture.Core.Generate;

namespace Conjecture.JsonSchema;

internal sealed class JsonSchemaStrategy : Strategy<JsonElement>
{
    private readonly Strategy<JsonElement> inner;

    internal JsonSchemaStrategy(JsonSchemaNode node, int maxDepth = 5)
    {
        inner = BuildStrategy(node, node.Defs, maxDepth, []);
    }

    internal override JsonElement Generate(ConjectureData data) => inner.Generate(data);

    private static Strategy<JsonElement> BuildStrategy(
        JsonSchemaNode node,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs,
        int depth,
        HashSet<string> visiting)
    {
        if (node.Ref is not null)
        {
            return BuildRefStrategy(node.Ref, defs, depth, visiting);
        }

        if (node.Const.HasValue)
        {
            JsonElement constValue = node.Const.Value;
            return Gen.Just(constValue);
        }

        if (node.Enum is not null && node.Enum.Count > 0)
        {
            IReadOnlyList<JsonElement> enumValues = node.Enum;
            return Gen.SampledFrom(enumValues);
        }

        return node.OneOf is not null && node.OneOf.Count > 0
            ? BuildOneOfStrategy(node.OneOf, defs, depth, visiting)
            : node.AnyOf is not null && node.AnyOf.Count > 0
            ? BuildOneOfStrategy(node.AnyOf, defs, depth, visiting)
            : node.AllOf is not null && node.AllOf.Count > 0
            ? BuildAllOfStrategy(node, node.AllOf, defs, depth, visiting)
            : node.Type switch
            {
                JsonSchemaType.Boolean => BuildBooleanStrategy(),
                JsonSchemaType.Integer => BuildIntegerStrategy(node),
                JsonSchemaType.Number => BuildNumberStrategy(node),
                JsonSchemaType.String => BuildStringStrategy(node),
                JsonSchemaType.Array => BuildArrayStrategy(node, defs, depth, visiting),
                JsonSchemaType.Object => BuildObjectStrategy(node, defs, depth, visiting),
                _ => BuildBooleanStrategy(),
            };
    }

    private static Strategy<JsonElement> BuildRefStrategy(
        string refValue,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs,
        int depth,
        HashSet<string> visiting)
    {
        JsonSchemaNode? resolved = JsonSchemaRefResolver.ResolveRef(refValue, defs);
        if (resolved is null)
        {
            return BuildBooleanStrategy();
        }

        if (visiting.Contains(refValue))
        {
            return BuildObjectStrategyAtBaseDepth(resolved, defs);
        }

        HashSet<string> newVisiting = [.. visiting, refValue];
        return BuildStrategy(resolved, defs ?? resolved.Defs, depth, newVisiting);
    }

    private static Strategy<JsonElement> BuildOneOfStrategy(
        IReadOnlyList<JsonSchemaNode> subSchemas,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs,
        int depth,
        HashSet<string> visiting)
    {
        Strategy<JsonElement>[] strategies = new Strategy<JsonElement>[subSchemas.Count];
        for (int i = 0; i < subSchemas.Count; i++)
        {
            strategies[i] = BuildStrategy(subSchemas[i], defs, depth, visiting);
        }

        return Gen.OneOf(strategies);
    }

    private static Strategy<JsonElement> BuildAllOfStrategy(
        JsonSchemaNode node,
        IReadOnlyList<JsonSchemaNode> allOf,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs,
        int depth,
        HashSet<string> visiting)
    {
        Dictionary<string, JsonSchemaNode> mergedProperties = [];
        HashSet<string> mergedRequiredSet = [];

        foreach (JsonSchemaNode sub in allOf)
        {
            if (sub.Properties is not null)
            {
                foreach (KeyValuePair<string, JsonSchemaNode> kv in sub.Properties)
                {
                    mergedProperties[kv.Key] = kv.Value;
                }
            }

            if (sub.Required is not null)
            {
                foreach (string r in sub.Required)
                {
                    mergedRequiredSet.Add(r);
                }
            }
        }

        List<string> mergedRequired = [.. mergedRequiredSet];

        JsonSchemaNode merged = node with
        {
            Type = JsonSchemaType.Object,
            Properties = mergedProperties.Count > 0 ? mergedProperties : null,
            Required = mergedRequired.Count > 0 ? mergedRequired : null,
            OneOf = null,
            AnyOf = null,
            AllOf = null,
        };

        return BuildObjectStrategy(merged, defs, depth, visiting);
    }

    private static Strategy<JsonElement> BuildBooleanStrategy()
    {
        return Gen.Booleans().Select(static b => b
            ? JsonSchemaRefResolver.TrueElement
            : JsonSchemaRefResolver.FalseElement);
    }

    private static Strategy<JsonElement> BuildIntegerStrategy(JsonSchemaNode node)
    {
        long min = node.Minimum ?? 0L;
        long max = node.Maximum ?? 1000L;
        if (node.ExclusiveMinimum)
        {
            min++;
        }

        if (node.ExclusiveMaximum)
        {
            max--;
        }

        return Gen.Integers(min, max).Select(static v => ParseLong(v));
    }

    private static Strategy<JsonElement> BuildNumberStrategy(JsonSchemaNode node)
    {
        double min = node.MinimumDouble ?? 0.0;
        double max = node.MaximumDouble ?? 1000.0;
        return Gen.Doubles(min, max).Select(static d => ParseDouble(d));
    }

    private static Strategy<JsonElement> BuildStringStrategy(JsonSchemaNode node)
    {
        if (node.Pattern is not null)
        {
            string pattern = node.Pattern;
            return Gen.Matching(pattern).Select(static s => ParseString(s));
        }

        return node.Format is not null
            ? node.Format switch
            {
                "email" => Gen.Email().Select(static s => ParseString(s)),
                "uri" => Gen.Url().Select(static s => ParseString(s)),
                "uuid" => Gen.Uuid().Select(static s => ParseString(s)),
                "date-time" => Gen.IsoDate().Select(static s => ParseString(s)),
                _ => BuildPlainStringStrategy(node),
            }
            : BuildPlainStringStrategy(node);
    }

    private static Strategy<JsonElement> BuildPlainStringStrategy(JsonSchemaNode node)
    {
        int minLen = node.MinLength ?? 0;
        int maxLen = node.MaxLength ?? 20;
        return Gen.Strings(minLen, maxLen).Select(static s => ParseString(s));
    }

    private static Strategy<JsonElement> BuildArrayStrategy(
        JsonSchemaNode node,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs,
        int depth,
        HashSet<string> visiting)
    {
        int minItems = node.MinItems ?? 0;
        int maxItems = node.MaxItems ?? 10;
        Strategy<JsonElement> itemStrategy = node.Items is not null
            ? BuildStrategy(node.Items, defs, depth, visiting)
            : BuildBooleanStrategy();

        return Gen.Lists(itemStrategy, minItems, maxItems).Select(static items =>
            JsonSerializer.SerializeToElement(items));
    }

    private static Strategy<JsonElement> BuildObjectStrategy(
        JsonSchemaNode node,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs,
        int depth,
        HashSet<string> visiting)
    {
        IReadOnlyDictionary<string, JsonSchemaNode> properties = node.Properties ?? EmptyProps;
        IReadOnlyList<string> required = node.Required ?? [];
        HashSet<string> requiredSet = [.. required];

        List<(string Name, Strategy<JsonElement> Strategy, bool IsRequired)> propStrategies = [];
        foreach (KeyValuePair<string, JsonSchemaNode> kv in properties)
        {
            bool isRequired = requiredSet.Contains(kv.Key);
            Strategy<JsonElement> propStrategy = BuildPropertyStrategy(kv.Value, defs, depth, visiting, isRequired);
            propStrategies.Add((kv.Key, propStrategy, isRequired));
        }

        return Gen.Compose(ctx =>
        {
            Dictionary<string, JsonElement> obj = [];
            foreach ((string name, Strategy<JsonElement> strategy, bool isRequired) in propStrategies)
            {
                bool include = isRequired || ctx.Generate(Gen.Booleans());
                if (include)
                {
                    obj[name] = ctx.Generate(strategy);
                }
            }

            return SerializeObject(obj);
        });
    }

    private static Strategy<JsonElement> BuildObjectStrategyAtBaseDepth(
        JsonSchemaNode node,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs)
    {
        IReadOnlyDictionary<string, JsonSchemaNode> properties = node.Properties ?? EmptyProps;
        IReadOnlyList<string> required = node.Required ?? [];
        HashSet<string> requiredSet = [.. required];

        List<(string Name, Strategy<JsonElement> Strategy)> propStrategies = [];
        foreach (KeyValuePair<string, JsonSchemaNode> kv in properties)
        {
            if (!requiredSet.Contains(kv.Key))
            {
                continue;
            }

            if (ContainsRef(kv.Value))
            {
                continue;
            }

            Strategy<JsonElement> propStrategy = BuildStrategy(kv.Value, defs, 0, []);
            propStrategies.Add((kv.Key, propStrategy));
        }

        return Gen.Compose(ctx =>
        {
            Dictionary<string, JsonElement> obj = [];
            foreach ((string name, Strategy<JsonElement> strategy) in propStrategies)
            {
                obj[name] = ctx.Generate(strategy);
            }

            return SerializeObject(obj);
        });
    }

    private static Strategy<JsonElement> BuildPropertyStrategy(
        JsonSchemaNode node,
        IReadOnlyDictionary<string, JsonSchemaNode>? defs,
        int depth,
        HashSet<string> visiting,
        bool isRequired)
    {
        if (node.Ref is not null && !isRequired)
        {
            string refValue = node.Ref;
            if (visiting.Contains(refValue) || depth <= 0)
            {
                return Gen.Just(JsonSchemaRefResolver.NullElement);
            }

            HashSet<string> newVisiting = [.. visiting, refValue];
            JsonSchemaNode? resolved = JsonSchemaRefResolver.ResolveRef(refValue, defs);
            return resolved is null
                ? Gen.Just(JsonSchemaRefResolver.NullElement)
                : BuildStrategy(resolved, defs ?? resolved.Defs, depth - 1, newVisiting);
        }

        return BuildStrategy(node, defs, depth, visiting);
    }

    private static bool ContainsRef(JsonSchemaNode node)
    {
        if (node.Ref is not null)
        {
            return true;
        }

        if (node.Properties is not null)
        {
            foreach (JsonSchemaNode child in node.Properties.Values)
            {
                if (ContainsRef(child))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static JsonElement ParseLong(long value) =>
        JsonSerializer.SerializeToElement(value);

    private static JsonElement ParseDouble(double value) =>
        JsonSerializer.SerializeToElement(value);

    private static JsonElement ParseString(string value) =>
        JsonSerializer.SerializeToElement(value);

    private static JsonElement SerializeObject(Dictionary<string, JsonElement> obj) =>
        JsonSerializer.SerializeToElement(obj);

    private static readonly IReadOnlyDictionary<string, JsonSchemaNode> EmptyProps =
        new Dictionary<string, JsonSchemaNode>();
}
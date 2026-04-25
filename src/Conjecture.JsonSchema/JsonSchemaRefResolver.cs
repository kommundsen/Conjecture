// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Text.Json;

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

    internal static readonly JsonElement TrueElement;
    internal static readonly JsonElement FalseElement;

    static JsonSchemaRefResolver()
    {
        TrueElement = JsonDocument.Parse("true").RootElement.Clone();
        FalseElement = JsonDocument.Parse("false").RootElement.Clone();
    }
}
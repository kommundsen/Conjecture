// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;

namespace Conjecture.Abstractions.JsonSchema;

/// <summary>Immutable representation of a parsed JSON Schema node.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record JsonSchemaNode(
    JsonSchemaType Type,
    IReadOnlyList<JsonSchemaNode>? OneOf,
    IReadOnlyList<JsonSchemaNode>? AnyOf,
    IReadOnlyList<JsonSchemaNode>? AllOf,
    IReadOnlyDictionary<string, JsonSchemaNode>? Properties,
    IReadOnlyList<string>? Required,
    JsonSchemaNode? Items,
    int? MinItems,
    int? MaxItems,
    long? Minimum,
    long? Maximum,
    double? MinimumDouble,
    double? MaximumDouble,
    bool ExclusiveMinimum,
    bool ExclusiveMaximum,
    int? MinLength,
    int? MaxLength,
    string? Pattern,
    IReadOnlyList<JsonElement>? Enum,
    JsonElement? Const,
    string? Ref,
    string? Format)
{
    /// <summary>Schema definitions ($defs or definitions), or null if none.</summary>
    public IReadOnlyDictionary<string, JsonSchemaNode>? Defs { get; init; }
}

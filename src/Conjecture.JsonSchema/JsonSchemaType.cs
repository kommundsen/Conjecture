// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.JsonSchema;

/// <summary>The JSON Schema primitive type of a schema node.</summary>
public enum JsonSchemaType
{
    /// <summary>No type constraint specified.</summary>
    None,

    /// <summary>JSON null.</summary>
    Null,

    /// <summary>JSON boolean.</summary>
    Boolean,

    /// <summary>JSON integer (subset of number).</summary>
    Integer,

    /// <summary>JSON number.</summary>
    Number,

    /// <summary>JSON string.</summary>
    String,

    /// <summary>JSON array.</summary>
    Array,

    /// <summary>JSON object.</summary>
    Object,

    /// <summary>Any type (unconstrained).</summary>
    Any,
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json.Serialization;

namespace Conjecture.Tool.Plan;

public class RefExpression
{
    [JsonPropertyName("$ref")]
    public required string Ref
    {
        get;
        init;
    }
}
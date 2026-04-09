// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json.Serialization;

namespace Conjecture.Tool.Plan;

public class PlanStep
{
    [JsonPropertyName("name")]
    public required string Name
    {
        get;
        init;
    }

    [JsonPropertyName("type")]
    public required string Type
    {
        get;
        init;
    }

    [JsonPropertyName("count")]
    public required int Count
    {
        get;
        init;
    }

    [JsonPropertyName("seed")]
    public ulong? Seed
    {
        get;
        init;
    }

    [JsonPropertyName("bindings")]
    public IDictionary<string, RefExpression>? Bindings
    {
        get;
        init;
    }
}
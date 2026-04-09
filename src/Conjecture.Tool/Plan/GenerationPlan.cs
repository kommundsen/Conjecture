// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json.Serialization;

namespace Conjecture.Tool.Plan;

public class GenerationPlan
{
    [JsonPropertyName("assembly")]
    public required string Assembly
    {
        get;
        init;
    }

    [JsonPropertyName("steps")]
    public required IReadOnlyList<PlanStep> Steps
    {
        get;
        init;
    }

    [JsonPropertyName("output")]
    public required OutputConfig Output
    {
        get;
        init;
    }
}
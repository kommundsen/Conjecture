// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json.Serialization;

namespace Conjecture.Tool.Plan;

public class OutputConfig
{
    [JsonPropertyName("format")]
    public required string Format
    {
        get;
        init;
    }

    [JsonPropertyName("file")]
    public string? File
    {
        get;
        init;
    }
}
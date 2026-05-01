// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Conjecture.Http;

namespace Conjecture.Aspire.Http;

/// <summary>Records HTTP interaction steps for failure trace reports.</summary>
public sealed class HttpInteractionTraceReporter
{
    private readonly List<RecordedStep> steps = [];

    /// <summary>Records a completed HTTP interaction step into the trace.</summary>
    public async Task Record(HttpInteraction interaction, HttpResponseMessage response, TimeSpan elapsed)
    {
        string responseBody = await response.Content.ReadAsStringAsync();
        steps.Add(new(interaction, (int)response.StatusCode, response.ReasonPhrase ?? string.Empty, responseBody, elapsed));
    }

    /// <summary>Formats the accumulated HTTP interaction trace as a human-readable string.</summary>
    public string FormatReport()
    {
        StringBuilder sb = new();

        if (steps.Count == 0)
        {
            sb.AppendLine("=== Shrunk interaction trace (0 steps) ===");
            sb.AppendLine("No interactions were recorded.");
        }
        else
        {
            sb.AppendLine($"=== Shrunk interaction trace ({steps.Count} steps) ===");

            for (int i = 0; i < steps.Count; i++)
            {
                RecordedStep step = steps[i];
                bool isLast = i == steps.Count - 1;

                string bodyPart = step.Interaction.Body is not null
                    ? $"  {step.Interaction.Body}"
                    : string.Empty;

                sb.AppendLine($"{i + 1}. {step.Interaction.Method,-4} {step.Interaction.ResourceName}/{step.Interaction.Path.TrimStart('/')}{bodyPart}");

                string annotation = isLast ? "   ← invariant violated" : string.Empty;
                sb.AppendLine($"   → {step.StatusCode} {step.ReasonPhrase} {step.ResponseBody}{annotation}");
            }
        }

        return sb.ToString();
    }

    private readonly record struct RecordedStep(
        HttpInteraction Interaction,
        int StatusCode,
        string ReasonPhrase,
        string ResponseBody,
        TimeSpan Elapsed);
}

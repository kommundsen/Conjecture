// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Aspire.Hosting;

namespace Conjecture.Aspire;

internal sealed class InteractionTraceReporter
{
    private readonly List<RecordedStep> steps = [];

    internal async Task Record(Interaction interaction, HttpResponseMessage response, TimeSpan elapsed)
    {
        string responseBody = await response.Content.ReadAsStringAsync();
        steps.Add(new(interaction, (int)response.StatusCode, response.ReasonPhrase ?? string.Empty, responseBody, elapsed));
    }

    internal string FormatReport(DistributedApplication? app)
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

        sb.AppendLine();
        sb.AppendLine("=== Service logs ===");

        if (app is null)
        {
            sb.AppendLine("(no application available)");
        }

        return sb.ToString();
    }

    private readonly record struct RecordedStep(
        Interaction Interaction,
        int StatusCode,
        string ReasonPhrase,
        string ResponseBody,
        TimeSpan Elapsed);
}
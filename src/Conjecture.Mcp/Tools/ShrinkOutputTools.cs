// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

using ModelContextProtocol.Server;

namespace Conjecture.Mcp.Tools;

[McpServerToolType]
internal static class ShrinkOutputTools
{
    private static readonly Regex SummaryLine =
        new(@"Falsifying example found after (?<examples>\d+) examples(?: \(shrunk (?<shrinks>\d+) times\))?", RegexOptions.Compiled);

    private static readonly Regex ShrunkFromLine =
        new(@"Shrunk (?<shrinks>\d+) times from original", RegexOptions.Compiled);

    private static readonly Regex ReproduceLine =
        new(@"Reproduce with: \[Property\(Seed = 0x(?<seed>[0-9A-Fa-f]+)\)\]", RegexOptions.Compiled);

    private static readonly Regex ParameterLine =
        new(@"^(?<indent>\s*)(?<name>\w+) = (?<value>.+)$", RegexOptions.Compiled);

    [McpServerTool(Name = "explain-shrink-output")]
    [Description(
        "Parses and explains a Conjecture test failure output. Returns a structured " +
        "explanation of what property failed, the original random input, the shrunk " +
        "minimal counterexample, and how to reproduce the failure.")]
    public static string ExplainShrinkOutput(
        [Description("The Conjecture failure output text from a failed [Property] test")] string failureOutput)
    {
        return string.IsNullOrWhiteSpace(failureOutput) ? "No output provided." : Parse(failureOutput);
    }

    internal static string Parse(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.TrimEntries);
        var sb = new StringBuilder();

        int? exampleCount = null;
        int? shrinkCount = null;
        string? seed = null;
        bool isExplicit = false;
        var originalParams = new List<(string Name, string Value)>();
        var shrunkParams = new List<(string Name, string Value)>();
        var currentSection = "unknown"; // "original", "shrunk", "explicit"

        foreach (var line in lines)
        {
            if (line.StartsWith("Explicit example failed:", StringComparison.Ordinal))
            {
                isExplicit = true;
                currentSection = "explicit";
                continue;
            }

            var summaryMatch = SummaryLine.Match(line);
            if (summaryMatch.Success)
            {
                exampleCount = int.Parse(summaryMatch.Groups["examples"].Value);
                if (summaryMatch.Groups["shrinks"].Success)
                {
                    shrinkCount = int.Parse(summaryMatch.Groups["shrinks"].Value);
                }

                currentSection = "original";
                continue;
            }

            var shrunkFromMatch = ShrunkFromLine.Match(line);
            if (shrunkFromMatch.Success)
            {
                shrinkCount ??= int.Parse(shrunkFromMatch.Groups["shrinks"].Value);
                continue;
            }

            if (line.StartsWith("Minimal counterexample:", StringComparison.Ordinal))
            {
                currentSection = "shrunk";
                continue;
            }

            var reproduceMatch = ReproduceLine.Match(line);
            if (reproduceMatch.Success)
            {
                seed = reproduceMatch.Groups["seed"].Value;
                continue;
            }

            var paramMatch = ParameterLine.Match(line);
            if (paramMatch.Success)
            {
                var param = (paramMatch.Groups["name"].Value, paramMatch.Groups["value"].Value);
                switch (currentSection)
                {
                    case "original": originalParams.Add(param); break;
                    case "shrunk": shrunkParams.Add(param); break;
                    case "explicit": originalParams.Add(param); break;
                }
            }
        }

        // Build explanation
        if (isExplicit)
        {
            sb.AppendLine("## Explicit Example Failure");
            sb.AppendLine();
            sb.AppendLine("A hand-written `[Sample]` seed value caused the property to fail.");
            if (originalParams.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**Failing values:**");
                foreach (var (name, value) in originalParams)
                {
                    sb.AppendLine($"- `{name}` = `{value}`");
                }
            }

            sb.AppendLine();
            sb.AppendLine("**What to do:** Check whether the failing seed reveals a genuine bug or whether the property assertion needs adjusting.");
            return sb.ToString();
        }

        if (exampleCount is null && originalParams.Count == 0 && seed is null)
        {
            sb.AppendLine("Could not parse Conjecture failure output. Expected format:");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("Falsifying example found after N examples (shrunk M times)");
            sb.AppendLine("param = value");
            sb.AppendLine("Minimal counterexample:");
            sb.AppendLine("param = value");
            sb.AppendLine("Reproduce with: [Property(Seed = 0x...)]");
            sb.AppendLine("```");
            return sb.ToString();
        }

        sb.AppendLine("## Conjecture Failure Explanation");
        sb.AppendLine();

        if (exampleCount.HasValue)
        {
            sb.AppendLine($"**Search:** Conjecture generated **{exampleCount} random examples** before finding a failure.");
            if (shrinkCount.HasValue)
            {
                sb.AppendLine($"**Shrinking:** The failing input was simplified **{shrinkCount} time{(shrinkCount == 1 ? "" : "s")}** to find the minimal counterexample.");
                if (shrinkCount == 0)
                {
                    sb.AppendLine("  (The first failing example was already minimal — no simplification was needed.)");
                }
            }
        }

        var displayParams = shrunkParams.Count > 0 ? shrunkParams : originalParams;
        if (displayParams.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(shrunkParams.Count > 0 ? "**Minimal counterexample (after shrinking):**" : "**Failing input:**");
            foreach (var (name, value) in displayParams)
            {
                sb.AppendLine($"- `{name}` = `{value}`");
            }

            sb.AppendLine();
            sb.AppendLine(
                "This is the **simplest** input Conjecture found that triggers the failure. " +
                "Start your debugging here — the actual bug may involve more complex inputs, but this minimal case isolates it.");
        }

        if (originalParams.Count > 0 && shrunkParams.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Original (unshrunk) failing input:**");
            foreach (var (name, value) in originalParams)
            {
                sb.AppendLine($"- `{name}` = `{value}`");
            }
        }

        if (seed is not null)
        {
            sb.AppendLine();
            sb.AppendLine("**Reproduction:**");
            sb.AppendLine($"Add `[Property(Seed = 0x{seed})]` to re-run this exact case deterministically:");
            sb.AppendLine("```csharp");
            sb.AppendLine($"[Property(Seed = 0x{seed})]");
            sb.AppendLine("public void YourTest(...) {{ ... }}");
            sb.AppendLine("```");
            sb.AppendLine("The example database also caches this failure — it will be retried first on future runs even without the seed attribute.");
        }

        return sb.ToString();
    }
}
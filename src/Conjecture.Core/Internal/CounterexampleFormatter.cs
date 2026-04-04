// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Globalization;
using System.Text;

namespace Conjecture.Core.Internal;

internal static class CounterexampleFormatter
{
    internal static string Format(IEnumerable<(string name, object value)> parameters, ulong seed)
    {
        StringBuilder sb = new();
        foreach ((string name, object value) in parameters)
        {
            sb.AppendLine($"{name} = {value}");
        }

        AppendReproduceLine(sb, seed);
        return sb.ToString();
    }

    internal static string Format(IEnumerable<(string name, object value)> parameters, ulong seed, int exampleCount, int shrinkCount, IReadOnlyDictionary<string, double>? targetingScores = null)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Falsifying example found after {exampleCount} examples");
        sb.AppendLine($"Shrunk {shrinkCount} times from original");
        foreach ((string name, object value) in parameters)
        {
            sb.AppendLine($"{name} = {FormatValue(value)}");
        }

        AppendTargetScores(sb, targetingScores);
        AppendReproduceLine(sb, seed);
        return sb.ToString();
    }

    internal static string Format(
        IEnumerable<(string name, object value)> originalParameters,
        IEnumerable<(string name, object value)> shrunkParameters,
        ulong seed,
        int exampleCount,
        int shrinkCount,
        IReadOnlyDictionary<string, double>? targetingScores = null)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Falsifying example found after {exampleCount} examples (shrunk {shrinkCount} times)");
        foreach ((string name, object value) in originalParameters)
        {
            sb.AppendLine($"{name} = {FormatValue(value)}");
        }

        if (shrinkCount > 0)
        {
            sb.AppendLine("Minimal counterexample:");
            foreach ((string name, object value) in shrunkParameters)
            {
                sb.AppendLine($"{name} = {FormatValue(value)}");
            }
        }

        AppendTargetScores(sb, targetingScores);
        AppendReproduceLine(sb, seed);
        return sb.ToString();
    }

    internal static string FormatExplicit(IEnumerable<(string name, object? value)> parameters, Exception failure)
    {
        StringBuilder sb = new();
        sb.AppendLine("Explicit example failed:");
        foreach ((string name, object? value) in parameters)
        {
            sb.AppendLine($"  {name} = {FormatValue(value)}");
        }

        sb.Append(failure.Message);
        return sb.ToString();
    }

    private static void AppendTargetScores(StringBuilder sb, IReadOnlyDictionary<string, double>? scores)
    {
        if (scores is null || scores.Count == 0)
        {
            return;
        }

        sb.AppendLine("Target scores:");
        foreach (string label in scores.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            sb.AppendLine($"  {label} = {scores[label].ToString("F4", CultureInfo.InvariantCulture)}");
        }
    }

    private static void AppendReproduceLine(StringBuilder sb, ulong seed)
    {
        sb.Append($"Reproduce with: [Property(Seed = 0x{seed:X})]");
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        Func<object, string>? fmt = FormatterRegistry.GetUntyped(value.GetType());
        return fmt is not null ? fmt(value) : value.ToString() ?? "null";
    }
}
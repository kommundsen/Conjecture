using System.Text;
using Conjecture.Core.Formatting;

namespace Conjecture.Core.Internal;

internal static class CounterexampleFormatter
{
    internal static string Format(IEnumerable<(string name, object value)> parameters, ulong seed)
    {
        var sb = new StringBuilder();
        foreach (var (name, value) in parameters)
        {
            sb.AppendLine($"{name} = {value}");
        }

        AppendReproduceLine(sb, seed);
        return sb.ToString();
    }

    internal static string Format(IEnumerable<(string name, object value)> parameters, ulong seed, int exampleCount, int shrinkCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Falsifying example found after {exampleCount} examples");
        sb.AppendLine($"Shrunk {shrinkCount} times from original");
        foreach (var (name, value) in parameters)
        {
            sb.AppendLine($"{name} = {FormatValue(value)}");
        }

        AppendReproduceLine(sb, seed);
        return sb.ToString();
    }

    internal static string Format(
        IEnumerable<(string name, object value)> originalParameters,
        IEnumerable<(string name, object value)> shrunkParameters,
        ulong seed,
        int exampleCount,
        int shrinkCount)
    {
        var sb = new StringBuilder();
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

        var fmt = FormatterRegistry.GetUntyped(value.GetType());
        return fmt is not null ? fmt(value) : value.ToString() ?? "null";
    }
}

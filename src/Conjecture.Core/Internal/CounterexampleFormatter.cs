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

        sb.Append($"Reproduce with: [Property(Seed = 0x{seed:X})]");
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

        sb.Append($"Reproduce with: [Property(Seed = 0x{seed:X})]");
        return sb.ToString();
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

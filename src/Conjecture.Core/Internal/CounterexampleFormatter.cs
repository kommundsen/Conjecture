using System.Text;

namespace Conjecture.Core.Internal;

internal static class CounterexampleFormatter
{
    internal static string Format(IEnumerable<(string name, object value)> parameters, ulong seed)
    {
        var sb = new StringBuilder();
        foreach (var (name, value) in parameters)
            sb.AppendLine($"{name} = {value}");
        sb.Append($"Reproduce with: [Property(Seed = 0x{seed:X})]");
        return sb.ToString();
    }
}

namespace Conjecture.Core.Internal;

internal static class StackTraceTrimmer
{
    private static readonly string[] FilteredPrefixes =
    [
        "Conjecture.Core.Internal.",
        "Conjecture.Xunit.Internal.",
        "System.RuntimeMethodHandle.",
        "System.Reflection.MethodBase.",
        "System.Reflection.MethodInvoker.",
    ];

    internal static string Trim(string? stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
        {
            return string.Empty;
        }

        string[] lines = stackTrace.Split(Environment.NewLine);
        List<string> kept = [];
        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();
            if (!trimmed.StartsWith("at ", StringComparison.Ordinal) || !IsFiltered(trimmed[3..]))
            {
                kept.Add(line);
            }
        }

        return string.Join(Environment.NewLine, kept);
    }

    private static bool IsFiltered(string qualifiedMethod)
    {
        foreach (string prefix in FilteredPrefixes)
        {
            if (qualifiedMethod.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.NUnit.Internal;

internal static class PropertyTestBuilder
{
    [RequiresUnreferencedCode("Accesses type and parameter metadata via reflection; not trim-safe.")]
    internal static string ComputeTestId(MethodInfo method)
    {
        StringBuilder sb = new();
        sb.Append(method.DeclaringType?.FullName ?? method.DeclaringType?.Name ?? "Unknown");
        sb.Append('.');
        sb.Append(method.Name);
        sb.Append('(');
        ParameterInfo[] parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) { sb.Append(','); }
            sb.Append(parameters[i].ParameterType.FullName ?? parameters[i].ParameterType.Name);
        }

        sb.Append(')');
        return TestIdHasher.Hash(sb.ToString());
    }

    [RequiresUnreferencedCode("Resolves parameter strategies by Type, which may be trimmed.")]
    internal static string BuildFailureMessage(TestRunResult result, ParameterInfo[] parameters)
    {
        IEnumerable<(string name, object value)> shrunkPairs = ResolvePairs(result.Counterexample!);
        string message = result.OriginalCounterexample is not null
            ? CounterexampleFormatter.Format(ResolvePairs(result.OriginalCounterexample), shrunkPairs, result.Seed!.Value, result.ExampleCount, result.ShrinkCount)
            : CounterexampleFormatter.Format(shrunkPairs, result.Seed!.Value, result.ExampleCount, result.ShrinkCount);
        string trimmedTrace = StackTraceTrimmer.Trim(result.FailureStackTrace);
        return string.IsNullOrEmpty(trimmedTrace) ? message : message + Environment.NewLine + trimmedTrace;

        IEnumerable<(string name, object value)> ResolvePairs(IReadOnlyList<IRNode> nodes)
        {
            ConjectureData replay = ConjectureData.ForRecord(nodes);
            object[] values = SharedParameterStrategyResolver.Resolve(parameters, replay);
            return parameters.Zip(values, (p, v) => (p.Name!, (object)v!));
        }
    }

    internal static void ValidateExampleArgs(ExampleAttribute example, ParameterInfo[] parameters)
    {
        if (example.Arguments.Length != parameters.Length)
        {
            throw new ArgumentException(
                $"[Example] provides {example.Arguments.Length} argument(s) but the method expects {parameters.Length}.");
        }
    }

    internal static string BuildExampleFailureMessage(ExampleAttribute example, ParameterInfo[] parameters, Exception failure)
    {
        IEnumerable<(string name, object? value)> pairs = parameters.Zip(example.Arguments, (p, a) => (p.Name!, a));
        return CounterexampleFormatter.FormatExplicit(pairs, failure);
    }
}

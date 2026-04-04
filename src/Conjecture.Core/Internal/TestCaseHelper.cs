// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Conjecture.Core.Internal;

internal static class TestCaseHelper
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
            ? CounterexampleFormatter.Format(ResolvePairs(result.OriginalCounterexample), shrunkPairs, result.Seed!.Value, result.ExampleCount, result.ShrinkCount, result.TargetingScores)
            : CounterexampleFormatter.Format(shrunkPairs, result.Seed!.Value, result.ExampleCount, result.ShrinkCount, result.TargetingScores);
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

    internal static bool IsAsyncReturnType(Type returnType)
    {
        return returnType == typeof(Task)
            || returnType == typeof(ValueTask)
            || (returnType.IsGenericType && (
                returnType.GetGenericTypeDefinition() == typeof(Task<>)
                || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)));
    }

    internal static void InvokeSync(MethodInfo method, object? instance, object[] args)
    {
        try
        {
            method.Invoke(instance, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    internal static async Task InvokeAsync(MethodInfo method, object? instance, object[] args)
    {
        object? returnVal;
        try
        {
            returnVal = method.Invoke(instance, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            return;
        }

        if (returnVal is Task task) { await task; }
        else if (returnVal is ValueTask vt) { await vt; }
    }
}
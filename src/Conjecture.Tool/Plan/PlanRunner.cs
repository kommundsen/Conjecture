// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

using Conjecture.Core;

namespace Conjecture.Tool.Plan;

public class PlanRunner
{
    private const string Int32TypeFull = "System.Int32";
    private const string Int32TypeSimple = "Int32";
    private const string StringTypeFull = "System.String";
    private const string StringTypeSimple = "String";

    private static readonly System.Reflection.MethodInfo RunProviderStrategyMethod =
        typeof(PlanRunner).GetMethod(nameof(RunProviderStrategy), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

    public PlanResult Run(GenerationPlan plan)
    {
        var stepResults = new Dictionary<string, IReadOnlyList<JsonElement>>();

        plan.Output.Validate();

        System.Reflection.Assembly assembly;
        try
        {
            assembly = System.Reflection.Assembly.LoadFrom(plan.Assembly);
        }
        catch (System.IO.FileNotFoundException)
        {
            throw new PlanException($"Assembly not found: {plan.Assembly}", exitCode: 1);
        }

        foreach (PlanStep step in plan.Steps)
        {
            IDictionary<string, IReadOnlyList<JsonElement>> resolvedBindings = new Dictionary<string, IReadOnlyList<JsonElement>>();
            if (step.Bindings is not null)
            {
                foreach (var binding in step.Bindings)
                {
                    string paramName = binding.Key;
                    string refPath = binding.Value.Ref;

                    try
                    {
                        IReadOnlyList<JsonElement> resolvedValues = RefResolver.Resolve(refPath, stepResults);
                        resolvedBindings[paramName] = resolvedValues;
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new PlanException($"Step '{step.Name}': {ex.Message}", exitCode: 1);
                    }
                }
            }

            object? data = GenerateStepData(step, resolvedBindings, assembly);

            IReadOnlyList<JsonElement> jsonElements = ConvertToJsonElements(data);
            stepResults[step.Name] = jsonElements;
        }

        return new PlanResult(stepResults);
    }

    private object? GenerateStepData(
        PlanStep step,
        IDictionary<string, IReadOnlyList<JsonElement>> resolvedBindings,
        System.Reflection.Assembly assembly)
    {
        ValidateBindingKeys(step, resolvedBindings);
        if (IsInt32(step.Type))
        {
            return GenerateBoundOrDefault(
                step,
                resolvedBindings,
                elem => elem.GetInt32(),
                () => Strategy.Integers<int>());
        }

        if (IsString(step.Type))
        {
            return GenerateBoundOrDefault(
                step,
                resolvedBindings,
                elem => elem.GetString()!,
                () => Strategy.Strings());
        }

        Type? providerType = AssemblyLoader.ResolveByTargetType(assembly, step.Type);
        if (providerType is null)
        {
            throw new PlanException($"No IStrategyProvider<T> found for type {step.Type}", exitCode: 1);
        }

        IStrategyProvider provider;
        try
        {
            provider = (IStrategyProvider)Activator.CreateInstance(providerType)!;
        }
        catch (MissingMethodException)
        {
            throw new PlanException(
                $"Provider for type {step.Type} has no public parameterless constructor",
                exitCode: 1);
        }
        Type targetType = AssemblyLoader.GetProviderTargetType(providerType)!;
        System.Reflection.MethodInfo method = RunProviderStrategyMethod.MakeGenericMethod(targetType);
        return method.Invoke(this, [provider, step]);
    }

    private object? RunProviderStrategy<T>(IStrategyProvider<T> provider, PlanStep step)
    {
        Strategy<T> strategy = provider.Create();
        return DataGen.Sample(strategy, step.Count, step.Seed);
    }

    private void ValidateBindingKeys(
        PlanStep step,
        IDictionary<string, IReadOnlyList<JsonElement>> resolvedBindings)
    {
        if (resolvedBindings.Count == 0)
        {
            return;
        }

        bool isKnownScalar = IsInt32(step.Type) || IsString(step.Type);

        if (!isKnownScalar)
        {
            return;
        }

        foreach (string key in resolvedBindings.Keys)
        {
            if (!string.Equals(key, "Value", StringComparison.Ordinal))
            {
                throw new PlanException(
                    $"Step '{step.Name}': unrecognized binding key '{key}' for type {step.Type}; recognized key is 'Value'",
                    exitCode: 1);
            }
        }
    }

    private static bool IsInt32(string typeName) =>
        string.Equals(typeName, Int32TypeFull, StringComparison.Ordinal) ||
        string.Equals(typeName, Int32TypeSimple, StringComparison.Ordinal);

    private static bool IsString(string typeName) =>
        string.Equals(typeName, StringTypeFull, StringComparison.Ordinal) ||
        string.Equals(typeName, StringTypeSimple, StringComparison.Ordinal);

    private object? GenerateBoundOrDefault<T>(
        PlanStep step,
        IDictionary<string, IReadOnlyList<JsonElement>> resolvedBindings,
        Func<JsonElement, T> extractor,
        Func<Strategy<T>> defaultStrategy)
    {
        if (resolvedBindings.Count > 0)
        {
            var allBoundValues = new List<T>();
            foreach (var binding in resolvedBindings.Values)
            {
                try
                {
                    foreach (var elem in binding)
                    {
                        allBoundValues.Add(extractor(elem));
                    }
                }
                catch (InvalidOperationException)
                {
                    throw new PlanException(
                        $"Step '{step.Name}': binding value type mismatch",
                        exitCode: 1);
                }
            }

            try
            {
                Strategy<T> strategy = Strategy.SampledFrom(allBoundValues);
                return DataGen.Sample(strategy, step.Count, step.Seed);
            }
            catch (ArgumentException)
            {
                throw new PlanException(
                    $"Step '{step.Name}': binding produced empty array",
                    exitCode: 1);
            }
        }
        else
        {
            return DataGen.Sample(defaultStrategy(), step.Count, step.Seed);
        }
    }

    private static IReadOnlyList<JsonElement> ConvertToJsonElements(object? data)
    {
        if (data is null)
        {
            return [];
        }

        var result = new List<JsonElement>();

        // Exclude string, which implements IEnumerable<char> but should be treated as a scalar
        if (data is System.Collections.IEnumerable enumerable && data is not string)
        {
            foreach (object? item in enumerable)
            {
                string itemJson = JsonSerializer.Serialize(item);
                using JsonDocument doc = JsonDocument.Parse(itemJson);
                result.Add(doc.RootElement.Clone());
            }
            return result;
        }

        // Single item (including bare string)
        string json = JsonSerializer.Serialize(data);
        using JsonDocument doc2 = JsonDocument.Parse(json);
        return new[] { doc2.RootElement.Clone() };
    }
}
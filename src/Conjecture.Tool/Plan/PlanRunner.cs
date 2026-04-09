// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections;
using System.Reflection;
using System.Text.Json;

using Conjecture.Core;

namespace Conjecture.Tool.Plan;

public class PlanRunner
{
    public PlanResult Run(GenerationPlan plan)
    {
        var stepResults = new Dictionary<string, IReadOnlyList<JsonElement>>();

        try
        {
            _ = Assembly.LoadFrom(plan.Assembly);
        }
        catch (FileNotFoundException)
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

            object? data = GenerateStepData(step, resolvedBindings);

            IReadOnlyList<JsonElement> jsonElements = ConvertToJsonElements(data);
            stepResults[step.Name] = jsonElements;
        }

        return new PlanResult(stepResults);
    }

    private object? GenerateStepData(
        PlanStep step,
        IDictionary<string, IReadOnlyList<JsonElement>> resolvedBindings)
    {
        ValidateBindingKeys(step, resolvedBindings);
        return step.Type == "System.Int32" || step.Type == "Int32"
            ? GenerateBoundOrDefault(
                step,
                resolvedBindings,
                elem => elem.GetInt32(),
                () => Generate.Integers<int>())
            : step.Type == "System.String" || step.Type == "String"
            ? GenerateBoundOrDefault(
                step,
                resolvedBindings,
                elem => elem.GetString()!,
                () => Generate.Strings())
            : throw new NotImplementedException($"Type {step.Type} is not yet supported");
    }

    private static void ValidateBindingKeys(
        PlanStep step,
        IDictionary<string, IReadOnlyList<JsonElement>> resolvedBindings)
    {
        if (resolvedBindings.Count == 0)
        {
            return;
        }

        bool isKnownScalar =
            step.Type == "System.Int32" || step.Type == "Int32" ||
            step.Type == "System.String" || step.Type == "String";

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
                Strategy<T> strategy = Generate.SampledFrom(allBoundValues);
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

        if (data is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                string itemJson = JsonSerializer.Serialize(item);
                using JsonDocument doc = JsonDocument.Parse(itemJson);
                result.Add(doc.RootElement.Clone());
            }
            return result;
        }

        // Single item
        string json = JsonSerializer.Serialize(data);
        using JsonDocument doc2 = JsonDocument.Parse(json);
        return new[] { doc2.RootElement.Clone() };
    }
}
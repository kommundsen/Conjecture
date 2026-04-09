// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Text.Json;

namespace Conjecture.Tool.Plan;

public static class RefResolver
{
    public static IReadOnlyList<JsonElement> Resolve(
        string refPath,
        IDictionary<string, IReadOnlyList<JsonElement>> priorResults)
    {
        // Parse the ref path: "customers[*].Id" or "config.Name"
        int bracketIndex = refPath.IndexOf('[');
        string stepName;
        string? propertyPath;
        bool isArrayExpansion;

        if (bracketIndex == -1)
        {
            // "config.Name" format - no array expansion
            int dotIndex = refPath.IndexOf('.');
            if (dotIndex == -1)
            {
                stepName = refPath;
                propertyPath = null;
            }
            else
            {
                stepName = refPath[..dotIndex];
                propertyPath = refPath[(dotIndex + 1)..];
            }
            isArrayExpansion = false;
        }
        else
        {
            // "customers[*].Id" format
            stepName = refPath[..bracketIndex];

            int closeBracketIndex = refPath.IndexOf(']', bracketIndex);
            if (closeBracketIndex == -1)
            {
                throw new InvalidOperationException($"Invalid ref format: {refPath}");
            }

            // Check if there's a property path after [*]
            propertyPath = closeBracketIndex + 1 < refPath.Length && refPath[closeBracketIndex + 1] == '.' ? refPath[(closeBracketIndex + 2)..] : null;
            isArrayExpansion = true;
        }

        // Look up the step in prior results
        if (!priorResults.TryGetValue(stepName, out IReadOnlyList<JsonElement>? stepResults))
        {
            throw new InvalidOperationException($"Reference to undefined step: {stepName}");
        }

        // If no array expansion and no property path, return the single item
        if (!isArrayExpansion && propertyPath is null)
        {
            return stepResults;
        }

        // Extract property from each element
        var resolved = new List<JsonElement>();

        if (isArrayExpansion)
        {
            // Array expansion: iterate over each element
            foreach (JsonElement element in stepResults)
            {
                if (propertyPath is null)
                {
                    // No property specified, add the element itself
                    resolved.Add(element);
                }
                else
                {
                    // Extract property from element
                    JsonElement value = ExtractProperty(element, propertyPath);
                    resolved.Add(value);
                }
            }
        }
        else
        {
            // No array expansion, but we have a property path
            if (stepResults.Count == 0)
            {
                throw new InvalidOperationException($"No results for step: {stepName}");
            }

            if (stepResults.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Step '{stepName}' produced {stepResults.Count} results; use '{stepName}[*].property' for multi-result steps");
            }

            JsonElement singleElement = stepResults[0];
            JsonElement value = ExtractProperty(singleElement, propertyPath!);
            resolved.Add(value);
        }

        return resolved;
    }

    private static JsonElement ExtractProperty(JsonElement element, string propertyPath)
    {
        JsonElement current = element;

        // Split by dot for nested properties
        string[] properties = propertyPath.Split('.');

        foreach (string prop in properties)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"Cannot extract property '{prop}' from non-object element");
            }

            if (!current.TryGetProperty(prop, out JsonElement nextElement))
            {
                throw new InvalidOperationException(
                    $"Property '{prop}' not found in element");
            }

            current = nextElement;
        }

        return current;
    }
}
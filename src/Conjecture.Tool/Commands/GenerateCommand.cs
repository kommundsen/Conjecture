// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;
using Conjecture.Formatters;

namespace Conjecture.Tool;

/// <summary>Generates values using a discovered strategy provider.</summary>
public static class GenerateCommand
{
    /// <summary>Executes data generation by loading an assembly, finding a provider, and formatting output.</summary>
    /// <param name="assemblyPath">Path to the assembly containing the strategy provider.</param>
    /// <param name="typeName">Target type name (full or simple name) for the strategy provider.</param>
    /// <param name="count">Number of values to generate.</param>
    /// <param name="seed">Optional seed for deterministic generation.</param>
    /// <param name="format">Output format name (e.g., "json", "jsonl").</param>
    /// <returns>The formatted output as a string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the type is not found or format is not supported.</exception>
    public static async Task<string> ExecuteAsync(
        string assemblyPath,
        string typeName,
        int count,
        ulong? seed,
        string format)
    {
        Assembly assembly = Assembly.LoadFrom(assemblyPath);
        Type? providerType = AssemblyLoader.ResolveByTargetType(assembly, typeName);

        if (providerType is null)
        {
            throw new InvalidOperationException($"Type provider not found for: {typeName}");
        }

        IOutputFormatter? formatter = GetFormatter(format);
        if (formatter is null)
        {
            throw new InvalidOperationException($"Unknown format: {format}");
        }

        // Dynamically invoke ExecuteTypedAsync<T> with the correct T
        Type? targetType = AssemblyLoader.GetProviderTargetType(providerType);
        if (targetType is null)
        {
            throw new InvalidOperationException($"Could not determine target type for provider: {providerType.Name}");
        }

        MethodInfo? executeMethod = typeof(GenerateCommand).GetMethod(
            nameof(ExecuteTypedAsync),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (executeMethod is null)
        {
            throw new InvalidOperationException("Failed to find ExecuteTypedAsync method");
        }

        MethodInfo genericExecuteMethod = executeMethod.MakeGenericMethod(targetType);
        object?[] args = new object?[] { providerType, count, seed, formatter };
        object? invocationResult = genericExecuteMethod.Invoke(null, args);
        Task<string> task = (Task<string>)(invocationResult ?? throw new InvalidOperationException("Failed to invoke ExecuteTypedAsync"));

        return await task;
    }

    private static IOutputFormatter? GetFormatter(string format)
    {
        return format switch
        {
            "json" => new JsonOutputFormatter(),
            "jsonl" => new JsonLinesOutputFormatter(),
            _ => null,
        };
    }

    private static async Task<string> ExecuteTypedAsync<T>(
        Type providerType,
        int count,
        ulong? seed,
        IOutputFormatter formatter)
    {
        object? providerInstance = Activator.CreateInstance(providerType);
        if (providerInstance is null)
        {
            throw new InvalidOperationException($"Failed to create instance of provider: {providerType.Name}");
        }

        var provider = (IStrategyProvider<T>)providerInstance;
        Strategy<T> strategy = provider.Create();
        IEnumerable<T> data = seed is { } s ? strategy.WithSeed(s).Stream(count) : strategy.Stream(count);

        using MemoryStream ms = new();
        await formatter.WriteAsync(data, ms);
        ms.Position = 0;
        using StreamReader reader = new(ms);
        return await reader.ReadToEndAsync();
    }
}
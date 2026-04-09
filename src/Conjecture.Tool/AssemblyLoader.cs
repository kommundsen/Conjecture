// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;

using Conjecture.Core;

namespace Conjecture.Tool;

/// <summary>Loads assemblies and discovers IStrategyProvider implementations.</summary>
public static class AssemblyLoader
{
    /// <summary>Loads an assembly and finds all types implementing IStrategyProvider&lt;T&gt;.</summary>
    /// <param name="assemblyPath">Path to the assembly file.</param>
    /// <returns>A read-only list of types implementing IStrategyProvider&lt;T&gt;.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the assembly file does not exist.</exception>
    public static IReadOnlyList<Type> FindProviders(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly not found: {assemblyPath}", assemblyPath);
        }

        Assembly assembly = Assembly.LoadFrom(assemblyPath);
        return FindProvidersInAssembly(assembly);
    }

    /// <summary>Finds IStrategyProvider&lt;T&gt; implementations in an assembly where T matches the given type name.</summary>
    /// <param name="assembly">The assembly to search.</param>
    /// <param name="typeName">The target type name (full name like "System.Int32" or simple name like "Int32").</param>
    /// <returns>The provider type if found; null otherwise.</returns>
    public static Type? ResolveByTargetType(Assembly assembly, string typeName)
    {
        IReadOnlyList<Type> providers = FindProvidersInAssembly(assembly);

        foreach (Type providerType in providers)
        {
            if (IsProviderForType(providerType, typeName))
            {
                return providerType;
            }
        }

        return null;
    }

    internal static IReadOnlyList<Type> FindProvidersInAssembly(Assembly assembly)
    {
        var providers = new List<Type>();

        foreach (Type type in assembly.GetTypes())
        {
            if (ImplementsIStrategyProvider(type))
            {
                providers.Add(type);
            }
        }

        return providers;
    }

    internal static Type? GetProviderTargetType(Type type)
    {
        foreach (Type iface in type.GetInterfaces())
        {
            if (iface.IsGenericType &&
                iface.GetGenericTypeDefinition() == typeof(IStrategyProvider<>))
            {
                return iface.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static bool ImplementsIStrategyProvider(Type type)
    {
        return GetProviderTargetType(type) is not null;
    }

    private static bool IsProviderForType(Type providerType, string typeName)
    {
        Type? targetType = GetProviderTargetType(providerType);
        return targetType is not null && (targetType.FullName == typeName || targetType.Name == typeName);
    }
}
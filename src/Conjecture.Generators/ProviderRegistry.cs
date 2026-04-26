// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Conjecture.Generators;

/// <summary>
/// Builds a compile-time registry mapping target types to their <c>IStrategyProvider&lt;T&gt;</c>
/// implementations, enabling AOT-safe member classification without runtime reflection.
/// </summary>
internal static class ProviderRegistry
{
    /// <summary>
    /// Scans all assemblies in <paramref name="compilation"/> for types whose name ends with
    /// <c>Arbitrary</c>, carry <c>[Arbitrary]</c>, and implement <c>IStrategyProvider&lt;T&gt;</c>.
    /// Returns a dictionary keyed by the fully-qualified target type name.
    /// </summary>
    internal static ImmutableDictionary<string, string> Build(Compilation compilation)
    {
        INamedTypeSymbol? openInterface =
            compilation.GetTypeByMetadataName("Conjecture.Core.IStrategyProvider`1");

        if (openInterface is null)
        {
            return ImmutableDictionary<string, string>.Empty;
        }

        ImmutableDictionary<string, string>.Builder builder =
            ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        CollectFromNamespace(compilation.Assembly.GlobalNamespace, openInterface, builder);

        foreach (MetadataReference reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
            {
                CollectFromNamespace(assembly.GlobalNamespace, openInterface, builder);
            }
        }

        return builder.ToImmutable();
    }

    private static void CollectFromNamespace(
        INamespaceSymbol ns,
        INamedTypeSymbol openInterface,
        ImmutableDictionary<string, string>.Builder builder)
    {
        foreach (INamedTypeSymbol type in ns.GetTypeMembers())
        {
            if (!type.Name.EndsWith("Arbitrary", StringComparison.Ordinal))
            {
                continue;
            }

            if (!SymbolHelpers.HasArbitraryAttribute(type))
            {
                continue;
            }

            foreach (INamedTypeSymbol iface in type.AllInterfaces)
            {
                if (!iface.IsGenericType)
                {
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, openInterface))
                {
                    continue;
                }

                ITypeSymbol targetType = iface.TypeArguments[0];
                string targetFqn = targetType.ToDisplayString(TypeModelExtractor.TypeNameFormat);
                string providerFqn = type.ToDisplayString(TypeModelExtractor.TypeNameFormat);

                if (!builder.ContainsKey(targetFqn))
                {
                    builder.Add(targetFqn, providerFqn);
                }

                break;
            }
        }

        foreach (INamespaceSymbol subNamespace in ns.GetNamespaceMembers())
        {
            CollectFromNamespace(subNamespace, openInterface, builder);
        }
    }

}
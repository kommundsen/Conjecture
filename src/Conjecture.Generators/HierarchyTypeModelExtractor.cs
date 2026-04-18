// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Conjecture.Generators;

internal static class HierarchyTypeModelExtractor
{
    internal static (HierarchyTypeModel? Model, ImmutableArray<Diagnostic> Diagnostics)
        Extract(INamedTypeSymbol baseSymbol, IEnumerable<INamedTypeSymbol> allArbitrarySymbols, Compilation? compilation = null)
    {
        ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        if (!baseSymbol.IsAbstract)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.Con300,
                baseSymbol.Locations.FirstOrDefault(),
                baseSymbol.Name));
            return (null, diagnostics.ToImmutable());
        }

        if (baseSymbol.TypeKind is not TypeKind.Class)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.Con301,
                baseSymbol.Locations.FirstOrDefault(),
                baseSymbol.Name,
                baseSymbol.TypeKind));
            return (null, diagnostics.ToImmutable());
        }

        ImmutableArray<string>.Builder typeParams = ImmutableArray.CreateBuilder<string>();
        foreach (ITypeParameterSymbol typeParam in baseSymbol.TypeParameters)
        {
            typeParams.Add(typeParam.Name);
        }

        // OriginalDefinition so generic Base<T> matches Base<int> in subtype chains
        string baseOriginalFqn = baseSymbol.OriginalDefinition.ToDisplayString(TypeModelExtractor.TypeNameFormat);
        string baseFullyQualifiedName = baseSymbol.ToDisplayString(TypeModelExtractor.TypeNameFormat);

        HashSet<string> processedFqns = [];

        ImmutableArray<SubtypeModel>.Builder subtypes = ImmutableArray.CreateBuilder<SubtypeModel>();
        foreach (INamedTypeSymbol arbitrarySymbol in allArbitrarySymbols)
        {
            if (arbitrarySymbol.IsAbstract)
            {
                continue;
            }

            if (!IsInHierarchy(arbitrarySymbol, baseOriginalFqn))
            {
                continue;
            }

            string fullyQualifiedName = arbitrarySymbol.ToDisplayString(TypeModelExtractor.TypeNameFormat);
            processedFqns.Add(fullyQualifiedName);

            if (!SymbolHelpers.HasArbitraryAttribute(arbitrarySymbol))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.Con205,
                    arbitrarySymbol.Locations.FirstOrDefault(),
                    arbitrarySymbol.Name,
                    baseSymbol.Name));
                continue;
            }

            subtypes.Add(new SubtypeModel(fullyQualifiedName, arbitrarySymbol.Name + "Arbitrary"));
        }

        if (compilation is not null)
        {
            WalkNamespace(compilation.GlobalNamespace, baseSymbol, baseOriginalFqn, diagnostics, processedFqns);
        }

        if (subtypes.Count == 0)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.Con302,
                baseSymbol.Locations.FirstOrDefault(),
                baseSymbol.Name));
            return (null, diagnostics.ToImmutable());
        }

        string baseNamespace = baseSymbol.ContainingNamespace.ToDisplayString();
        string baseTypeName = baseSymbol.Name;

        HierarchyTypeModel model = new(
            FullyQualifiedName: baseFullyQualifiedName,
            Namespace: baseNamespace,
            TypeName: baseTypeName,
            TypeParameters: typeParams.ToImmutable(),
            Subtypes: subtypes.ToImmutable());

        return (model, diagnostics.ToImmutable());
    }

    private static bool IsInHierarchy(INamedTypeSymbol symbol, string baseOriginalFqn)
    {
        INamedTypeSymbol? current = symbol.BaseType;
        while (current is not null)
        {
            if (current.OriginalDefinition.ToDisplayString(TypeModelExtractor.TypeNameFormat) == baseOriginalFqn)
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static void WalkNamespace(
        INamespaceSymbol ns,
        INamedTypeSymbol baseSymbol,
        string baseOriginalFqn,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        HashSet<string> processedFqns)
    {
        foreach (INamedTypeSymbol typeSymbol in ns.GetTypeMembers())
        {
            CheckTypeForMissingArbitraryAttribute(typeSymbol, baseSymbol, baseOriginalFqn, diagnostics, processedFqns);
        }

        foreach (INamespaceSymbol childNs in ns.GetNamespaceMembers())
        {
            WalkNamespace(childNs, baseSymbol, baseOriginalFqn, diagnostics, processedFqns);
        }
    }

    private static void CheckTypeForMissingArbitraryAttribute(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol baseSymbol,
        string baseOriginalFqn,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        HashSet<string> processedFqns)
    {
        if (typeSymbol.IsAbstract)
        {
            return;
        }

        if (!IsInHierarchy(typeSymbol, baseOriginalFqn))
        {
            foreach (INamedTypeSymbol nestedType in typeSymbol.GetTypeMembers())
            {
                CheckTypeForMissingArbitraryAttribute(nestedType, baseSymbol, baseOriginalFqn, diagnostics, processedFqns);
            }

            return;
        }

        string typeFqn = typeSymbol.ToDisplayString(TypeModelExtractor.TypeNameFormat);
        if (!processedFqns.Add(typeFqn))
        {
            return;
        }

        if (!SymbolHelpers.HasArbitraryAttribute(typeSymbol))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.Con205,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name,
                baseSymbol.Name));
        }

        foreach (INamedTypeSymbol nestedType in typeSymbol.GetTypeMembers())
        {
            CheckTypeForMissingArbitraryAttribute(nestedType, baseSymbol, baseOriginalFqn, diagnostics, processedFqns);
        }
    }
}
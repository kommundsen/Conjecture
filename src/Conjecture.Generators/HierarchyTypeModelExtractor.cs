// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace Conjecture.Generators;

internal static class HierarchyTypeModelExtractor
{
    internal static (HierarchyTypeModel? Model, ImmutableArray<Diagnostic> Diagnostics)
        Extract(INamedTypeSymbol baseSymbol, IEnumerable<INamedTypeSymbol> allArbitrarySymbols)
    {
        ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // Validate base is abstract
        if (!baseSymbol.IsAbstract)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.Con300,
                baseSymbol.Locations.FirstOrDefault(),
                baseSymbol.Name));
            return (null, diagnostics.ToImmutable());
        }

        // Validate base is class or record (not interface or struct)
        if (baseSymbol.TypeKind is not TypeKind.Class)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.Con301,
                baseSymbol.Locations.FirstOrDefault(),
                baseSymbol.Name,
                baseSymbol.TypeKind));
            return (null, diagnostics.ToImmutable());
        }

        // Collect type parameters from base
        ImmutableArray<string>.Builder typeParams = ImmutableArray.CreateBuilder<string>();
        foreach (ITypeParameterSymbol typeParam in baseSymbol.TypeParameters)
        {
            typeParams.Add(typeParam.Name);
        }

        // Get fully qualified name of base for comparison (use OriginalDefinition so generic Base<T> matches Base<int> in subtype chains)
        string baseOriginalFqn = baseSymbol.OriginalDefinition.ToDisplayString(TypeModelExtractor.TypeNameFormat);
        string baseFullyQualifiedName = baseSymbol.ToDisplayString(TypeModelExtractor.TypeNameFormat);

        // Filter arbitrary symbols to those in the base's type hierarchy and not abstract
        ImmutableArray<SubtypeModel>.Builder subtypes = ImmutableArray.CreateBuilder<SubtypeModel>();
        foreach (INamedTypeSymbol arbitrarySymbol in allArbitrarySymbols)
        {
            // Skip abstract types
            if (arbitrarySymbol.IsAbstract)
            {
                continue;
            }

            // Check if arbitrarySymbol extends baseSymbol using display-name comparison so it works across compilations
            INamedTypeSymbol? currentBase = arbitrarySymbol.BaseType;
            bool isInHierarchy = false;

            while (currentBase is not null)
            {
                if (currentBase.OriginalDefinition.ToDisplayString(TypeModelExtractor.TypeNameFormat) == baseOriginalFqn)
                {
                    isInHierarchy = true;
                    break;
                }

                currentBase = currentBase.BaseType;
            }

            if (isInHierarchy)
            {
                string providerTypeName = arbitrarySymbol.Name + "Arbitrary";
                string fullyQualifiedName = arbitrarySymbol.ToDisplayString(TypeModelExtractor.TypeNameFormat);
                subtypes.Add(new SubtypeModel(fullyQualifiedName, providerTypeName));
            }
        }

        // Return null if no concrete subtypes found
        if (subtypes.Count == 0)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.Con302,
                baseSymbol.Locations.FirstOrDefault(),
                baseSymbol.Name));
            return (null, diagnostics.ToImmutable());
        }

        // Build the model
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
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Analyzers;

internal static class PropertyAttributeHelper
{
    internal static bool HasPropertyAttribute(MethodDeclarationSyntax method, SemanticModel model)
    {
        INamedTypeSymbol? markerInterface =
            model.Compilation.GetTypeByMetadataName("Conjecture.Core.IPropertyTest");

        foreach (AttributeListSyntax attrList in method.AttributeLists)
        {
            foreach (AttributeSyntax attr in attrList.Attributes)
            {
                SymbolInfo info = model.GetSymbolInfo(attr);
                ISymbol? symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                INamedTypeSymbol? attrType = symbol?.ContainingType;

                if (attrType is not null && markerInterface is not null)
                {
                    foreach (INamedTypeSymbol iface in attrType.AllInterfaces)
                    {
                        if (SymbolEqualityComparer.Default.Equals(iface, markerInterface))
                        {
                            return true;
                        }
                    }
                }
                else if (attrType?.Name == "PropertyAttribute")
                {
                    return true;
                }

                // Fallback: match by name when the type is not fully resolvable
                string name = attr.Name.ToString();
                if (name is "Property" or "PropertyAttribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool HasFromAttribute(ParameterSyntax parameter, SemanticModel model)
    {
        foreach (AttributeListSyntax attrList in parameter.AttributeLists)
        {
            foreach (AttributeSyntax attr in attrList.Attributes)
            {
                SymbolInfo info = model.GetSymbolInfo(attr);
                ISymbol? symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                if (symbol?.ContainingType?.MetadataName == "FromAttribute`1")
                {
                    return true;
                }

                // Fallback: name-based when attribute is unresolvable
                string name = attr.Name.ToString();
                if (name.StartsWith("From<", System.StringComparison.Ordinal) || name == "From")
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool HasArbitraryAttribute(ITypeSymbol typeSymbol)
    {
        foreach (AttributeData attr in typeSymbol.GetAttributes())
        {
            INamedTypeSymbol? attrClass = attr.AttributeClass;
            if (attrClass?.Name is "ArbitraryAttribute" or "Arbitrary" &&
                attrClass.ContainingNamespace?.ToDisplayString() == "Conjecture.Core")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the unqualified type name of <c>T</c> in a <c>[From&lt;T&gt;]</c> attribute,
    /// or <see langword="null"/> if no such attribute is present.
    /// </summary>
    internal static string? TryGetFromStrategyTypeName(ParameterSyntax parameter)
    {
        foreach (AttributeListSyntax attrList in parameter.AttributeLists)
        {
            foreach (AttributeSyntax attr in attrList.Attributes)
            {
                string name = attr.Name.ToString();
                if (name.StartsWith("From<", System.StringComparison.Ordinal) && name.EndsWith(">"))
                {
                    return name.Substring(5, name.Length - 6);
                }
            }
        }

        return null;
    }
}
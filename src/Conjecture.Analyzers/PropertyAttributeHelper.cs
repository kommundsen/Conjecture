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
}
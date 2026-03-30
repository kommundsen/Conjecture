using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Analyzers;

internal static class PropertyAttributeHelper
{
    internal static bool HasPropertyAttribute(MethodDeclarationSyntax method, SemanticModel model)
    {
        foreach (AttributeListSyntax attrList in method.AttributeLists)
        {
            foreach (AttributeSyntax attr in attrList.Attributes)
            {
                SymbolInfo info = model.GetSymbolInfo(attr);
                ISymbol? symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                if (symbol?.ContainingType?.Name == "PropertyAttribute")
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

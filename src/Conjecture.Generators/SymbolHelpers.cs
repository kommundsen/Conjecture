// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;

namespace Conjecture.Generators;

internal static class SymbolHelpers
{
    /// <summary>Returns <see langword="true"/> if <paramref name="symbol"/> carries <c>[Conjecture.Core.Arbitrary]</c>.</summary>
    internal static bool HasArbitraryAttribute(INamedTypeSymbol symbol)
    {
        foreach (AttributeData attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass is
                {
                    Name: "ArbitraryAttribute",
                    ContainingNamespace.Name: "Core",
                    ContainingNamespace.ContainingNamespace.Name: "Conjecture",
                })
            {
                return true;
            }
        }

        return false;
    }
}

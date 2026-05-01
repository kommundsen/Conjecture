// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Analyzers;

internal static class GenBoundsHelper
{
    internal static bool TryFindMinMaxArgIndices(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        out int minArgIndex,
        out int maxArgIndex)
    {
        minArgIndex = -1;
        maxArgIndex = -1;

        int minParamIndex = -1, maxParamIndex = -1;
        string? minName = null, maxName = null;

        foreach (IParameterSymbol param in method.Parameters)
        {
            if (param.Name is "min" or "minLength" or "minSize")
            {
                minName = param.Name;
                minParamIndex = param.Ordinal;
            }
            else if (param.Name is "max" or "maxLength" or "maxSize")
            {
                maxName = param.Name;
                maxParamIndex = param.Ordinal;
            }
        }

        if (minParamIndex < 0 || maxParamIndex < 0)
        {
            return false;
        }

        SeparatedSyntaxList<ArgumentSyntax> args = invocation.ArgumentList.Arguments;
        int foundMin = FindArgIndex(args, minName!, minParamIndex);
        int foundMax = FindArgIndex(args, maxName!, maxParamIndex);

        if (foundMin < 0 || foundMax < 0)
        {
            return false;
        }

        minArgIndex = foundMin;
        maxArgIndex = foundMax;
        return true;
    }

    internal static int FindArgIndex(
        SeparatedSyntaxList<ArgumentSyntax> args,
        string paramName,
        int paramIndex)
    {
        bool anyNamed = false;

        for (int i = 0; i < args.Count; i++)
        {
            string? name = args[i].NameColon?.Name.Identifier.Text;

            if (name == paramName)
            {
                return i;
            }

            if (name != null)
            {
                anyNamed = true;
            }
        }

        return (!anyNamed && paramIndex < args.Count) ? paramIndex : -1;
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CON103Analyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CON103",
        title: "Strategy bounds are inverted",
        messageFormat: "min ({0}) is greater than max ({1}); swap the arguments",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The minimum bound is greater than the maximum bound for this strategy.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        string methodName = memberAccess.Name.Identifier.Text;
        if (methodName is not ("Integers" or "Doubles" or "Floats" or "Strings"))
        {
            return;
        }

        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType.ToDisplayString() != "Conjecture.Core.Generate")
        {
            return;
        }

        if (!GenBoundsHelper.TryFindMinMaxArgIndices(invocation, method, out int minIdx, out int maxIdx))
        {
            return;
        }

        SeparatedSyntaxList<ArgumentSyntax> args = invocation.ArgumentList.Arguments;
        ExpressionSyntax minExpr = args[minIdx].Expression;
        ExpressionSyntax maxExpr = args[maxIdx].Expression;

        Optional<object?> minConst = context.SemanticModel.GetConstantValue(minExpr);
        Optional<object?> maxConst = context.SemanticModel.GetConstantValue(maxExpr);

        if (!minConst.HasValue || !maxConst.HasValue)
        {
            return;
        }

        if (!TryConvertToDouble(minConst.Value, out double minVal) ||
            !TryConvertToDouble(maxConst.Value, out double maxVal))
        {
            return;
        }

        if (minVal > maxVal)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), minVal, maxVal));
        }
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;
        if (value is null)
        {
            return false;
        }

        try
        {
            result = Convert.ToDouble(value);
            return true;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
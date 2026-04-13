// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CON108Analyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CON108",
        title: "Assume.That condition is always true given strategy constraint",
        messageFormat: "Assume.That condition is always true for parameter '{0}' given strategy '{1}'; remove the redundant assumption",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The strategy guarantees the parameter already satisfies this condition; the Assume.That call is redundant.");

    // Maps strategy class name → (operator kind, threshold) that the strategy guarantees.
    private static readonly Dictionary<string, (SyntaxKind Op, int Threshold)> KnownStrategies = new()
    {
        ["PositiveInts"] = (SyntaxKind.GreaterThanExpression, 0),
        ["NegativeInts"] = (SyntaxKind.LessThanExpression, 0),
        ["NonNegativeInts"] = (SyntaxKind.GreaterThanOrEqualExpression, 0),
    };

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
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsAssumeThat(invocation, context.SemanticModel))
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        MethodDeclarationSyntax? method = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return;
        }

        if (!PropertyAttributeHelper.HasPropertyAttribute(method, context.SemanticModel))
        {
            return;
        }

        ExpressionSyntax condition = invocation.ArgumentList.Arguments[0].Expression;
        if (condition is not BinaryExpressionSyntax binary)
        {
            return;
        }

        if (binary.Left is not IdentifierNameSyntax paramRef)
        {
            return;
        }

        if (binary.Right is not LiteralExpressionSyntax literal ||
            !literal.Token.IsKind(SyntaxKind.NumericLiteralToken))
        {
            return;
        }

        string referencedParam = paramRef.Identifier.Text;
        SyntaxKind conditionOp = binary.Kind();
        int conditionThreshold = (int)literal.Token.Value!;

        foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
        {
            if (parameter.Identifier.Text != referencedParam)
            {
                continue;
            }

            string? strategyName = PropertyAttributeHelper.TryGetFromStrategyTypeName(parameter);
            if (strategyName is null)
            {
                break;
            }

            if (!KnownStrategies.TryGetValue(strategyName, out (SyntaxKind Op, int Threshold) constraint))
            {
                break;
            }

            if (IsImplied(constraint.Op, constraint.Threshold, conditionOp, conditionThreshold))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, invocation.GetLocation(), referencedParam, strategyName));
            }

            break;
        }
    }

    private static bool IsAssumeThat(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (memberAccess.Name.Identifier.Text != "That")
        {
            return false;
        }

        SymbolInfo info = model.GetSymbolInfo(invocation);
        if (info.Symbol is IMethodSymbol method)
        {
            return method.ContainingType.ToDisplayString() == "Conjecture.Core.Assume";
        }

        // Fallback when semantic resolution is unavailable
        return memberAccess.Expression.ToString() == "Assume";
    }

    // Returns true when the constraint (constraintOp, constraintThreshold) guarantees
    // that the condition (conditionOp, conditionThreshold) is always satisfied.
    private static bool IsImplied(
        SyntaxKind constraintOp,
        int constraintThreshold,
        SyntaxKind conditionOp,
        int conditionThreshold)
    {
        return (constraintOp, conditionOp) switch
        {
            // x > k implies x > n  iff  k >= n
            (SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanExpression)
                => constraintThreshold >= conditionThreshold,

            // x < k implies x < n  iff  k <= n
            (SyntaxKind.LessThanExpression, SyntaxKind.LessThanExpression)
                => constraintThreshold <= conditionThreshold,

            // x >= k implies x >= n  iff  k >= n
            (SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.GreaterThanOrEqualExpression)
                => constraintThreshold >= conditionThreshold,

            // x > k implies x >= n  iff  k+1 >= n  (i.e. k >= n-1)
            (SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanOrEqualExpression)
                => constraintThreshold + 1 >= conditionThreshold,

            _ => false,
        };
    }
}
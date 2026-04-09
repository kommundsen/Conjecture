// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CON101Analyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CON101",
        title: "High-rejection .Where() predicate",
        messageFormat: "This .Where() predicate will reject most generated values, causing poor performance or test failures",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Predicates that match very few values cause the test engine to generate many examples before finding valid ones.");

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

        if (memberAccess.Name.Identifier.Text != "Where")
        {
            return;
        }

        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType.ToDisplayString() != "Conjecture.Core.StrategyExtensions")
        {
            return;
        }

        SeparatedSyntaxList<ArgumentSyntax> args = invocation.ArgumentList.Arguments;
        if (args.Count != 1)
        {
            return;
        }

        ExpressionSyntax predicate = args[0].Expression;
        ExpressionSyntax? body = predicate switch
        {
            SimpleLambdaExpressionSyntax simple => simple.ExpressionBody,
            ParenthesizedLambdaExpressionSyntax paren => paren.ExpressionBody,
            _ => null
        };

        if (body is null)
        {
            return;
        }

        if (IsHighRejection(body, context))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }

    private static bool IsHighRejection(ExpressionSyntax body, SyntaxNodeAnalysisContext context)
    {
        if (body.IsKind(SyntaxKind.FalseLiteralExpression))
        {
            return true;
        }

        if (body is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.EqualsExpression))
        {
            Optional<object?> leftConst = context.SemanticModel.GetConstantValue(binary.Left);
            Optional<object?> rightConst = context.SemanticModel.GetConstantValue(binary.Right);
            return leftConst.HasValue || rightConst.HasValue;
        }

        return false;
    }
}
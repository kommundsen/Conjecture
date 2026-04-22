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
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (memberAccess.Name.Identifier.Text != "Where")
        {
            return;
        }

        if (!IsStrategyReceiver(context.SemanticModel, memberAccess))
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

    private static bool IsStrategyReceiver(SemanticModel model, MemberAccessExpressionSyntax memberAccess)
    {
        ITypeSymbol? receiverType = model.GetTypeInfo(memberAccess.Expression).Type;
        if (IsStrategyType(receiverType))
        {
            return true;
        }

        SymbolInfo symbolInfo = model.GetSymbolInfo(memberAccess);
        if (symbolInfo.Symbol is IMethodSymbol sym && IsStrategyExtensionsContaining(sym.ContainingType))
        {
            return true;
        }

        foreach (ISymbol candidate in symbolInfo.CandidateSymbols)
        {
            if (candidate is IMethodSymbol cm && IsStrategyExtensionsContaining(cm.ContainingType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsStrategyType(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol named &&
               named.Name == "Strategy" &&
               named.IsGenericType &&
               named.ContainingNamespace.ToDisplayString() == "Conjecture.Core";
    }

    private static bool IsStrategyExtensionsContaining(INamedTypeSymbol type)
    {
        if (type.ToDisplayString() == "Conjecture.Core.StrategyExtensions")
        {
            return true;
        }

        // C# 14 extension block: method's containing type is the extension group nested inside StrategyExtensions
        return type.ContainingType is not null &&
               type.ContainingType.ToDisplayString() == "Conjecture.Core.StrategyExtensions";
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
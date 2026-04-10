// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CJ0050Analyzer : DiagnosticAnalyzer
{
    internal const string PropertyNameKey = "PropertyName";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CJ0050",
        title: "Suggest named extension property",
        messageFormat: "Use .{0} instead of .Where({1})",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "A common .Where() predicate pattern has a named extension property equivalent.");

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

        if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        ExpressionSyntax argument = invocation.ArgumentList.Arguments[0].Expression;
        if (argument is not LambdaExpressionSyntax lambda)
        {
            return;
        }

        if (!IsStrategyReceiver(context.SemanticModel, invocation, memberAccess))
        {
            return;
        }

        string? paramName = GetLambdaParamName(lambda);
        if (paramName is null)
        {
            return;
        }

        ExpressionSyntax? body = GetLambdaBody(lambda);
        if (body is null)
        {
            return;
        }

        string? propertyName = MatchPattern(body, paramName);
        if (propertyName is null)
        {
            return;
        }

        string predicateText = argument.ToString();
        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add(PropertyNameKey, propertyName);

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            properties,
            propertyName,
            predicateText));
    }

    private static bool IsStrategyReceiver(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess)
    {
        ITypeSymbol? receiverType = model.GetTypeInfo(memberAccess.Expression).Type;
        if (IsStrategyType(receiverType))
        {
            return true;
        }

        SymbolInfo symbolInfo = model.GetSymbolInfo(memberAccess);
        if (symbolInfo.Symbol is IMethodSymbol sym && IsStrategyType(sym.ContainingType))
        {
            return true;
        }

        foreach (ISymbol candidate in symbolInfo.CandidateSymbols)
        {
            if (candidate is IMethodSymbol cm && IsStrategyType(cm.ContainingType))
            {
                return true;
            }
        }

        ITypeSymbol? returnType = model.GetTypeInfo(invocation).Type;
        return IsStrategyType(returnType) || IsStrategyTypeSyntactically(invocation);
    }

    private static bool IsStrategyType(ITypeSymbol? type)
    {
        return type is not null && type.Name == "Strategy" && type is INamedTypeSymbol { IsGenericType: true };
    }

    private static bool IsStrategyTypeSyntactically(SyntaxNode node)
    {
        return node.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax decl } }
            && IsStrategyTypeSyntax(decl.Type);
    }

    private static bool IsStrategyTypeSyntax(TypeSyntax type)
    {
        return type is GenericNameSyntax generic && generic.Identifier.Text == "Strategy"
            || type is QualifiedNameSyntax qualified && IsStrategyTypeSyntax(qualified.Right);
    }

    private static string? GetLambdaParamName(LambdaExpressionSyntax lambda)
    {
        return lambda is SimpleLambdaExpressionSyntax simple
            ? simple.Parameter.Identifier.Text
            : lambda is ParenthesizedLambdaExpressionSyntax paren &&
              paren.ParameterList.Parameters.Count == 1
              ? paren.ParameterList.Parameters[0].Identifier.Text
              : null;
    }

    private static ExpressionSyntax? GetLambdaBody(LambdaExpressionSyntax lambda)
    {
        return lambda is SimpleLambdaExpressionSyntax simple &&
            simple.Body is ExpressionSyntax simpleExpr
            ? simpleExpr
            : lambda is ParenthesizedLambdaExpressionSyntax paren &&
              paren.Body is ExpressionSyntax parenExpr
              ? parenExpr
              : null;
    }

    private static string? MatchPattern(ExpressionSyntax body, string paramName)
    {
        if (body is not BinaryExpressionSyntax binary)
        {
            return null;
        }

        string leftText = binary.Left.ToString();
        string rightText = binary.Right.ToString();
        SyntaxKind op = binary.OperatorToken.Kind();

        return (leftText, op, rightText) switch
        {
            _ when leftText == paramName && op == SyntaxKind.GreaterThanToken && rightText == "0" => "Positive",
            _ when leftText == paramName && op == SyntaxKind.LessThanToken && rightText == "0" => "Negative",
            _ when leftText == paramName && op == SyntaxKind.ExclamationEqualsToken && rightText == "0" => "NonZero",
            _ when leftText == paramName + ".Length" && op == SyntaxKind.GreaterThanToken && rightText == "0" => "NonEmpty",
            _ when leftText == paramName + ".Count" && op == SyntaxKind.GreaterThanToken && rightText == "0" => "NonEmpty",
            _ => null,
        };
    }
}
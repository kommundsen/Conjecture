// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CON111Analyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CON111",
        title: "Target.Maximize/Minimize has no effect outside a [Property] method",
        messageFormat: "'{0}' has no effect outside a [Property] method; move this call inside a [Property] method",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Target.Maximize and Target.Minimize only affect generation when called inside a [Property] method body.");

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

        if (!IsTargetCall(invocation, context.SemanticModel, out string? methodName))
        {
            return;
        }

        MethodDeclarationSyntax? method = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();

        if (method is not null && PropertyAttributeHelper.HasPropertyAttribute(method, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }

    private static bool IsTargetCall(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        out string? methodName)
    {
        methodName = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        string name = memberAccess.Name.Identifier.Text;
        if (name != "Maximize" && name != "Minimize")
        {
            return false;
        }

        SymbolInfo info = model.GetSymbolInfo(invocation);
        ISymbol? symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        if (symbol is IMethodSymbol methodSymbol)
        {
            if (methodSymbol.ContainingType.ToDisplayString() == "Conjecture.Core.Target")
            {
                methodName = name;
                return true;
            }

            return false;
        }

        // Fallback when semantic resolution is unavailable.
        // Check the receiver type: if it resolves to a type other than Conjecture.Core.Target, reject.
        if (memberAccess.Expression.ToString() == "Target")
        {
            TypeInfo receiverType = model.GetTypeInfo(memberAccess.Expression);
            if (receiverType.Type is INamedTypeSymbol receiverSymbol &&
                receiverSymbol.ToDisplayString() != "Conjecture.Core.Target")
            {
                return false;
            }

            methodName = name;
            return true;
        }

        return false;
    }
}
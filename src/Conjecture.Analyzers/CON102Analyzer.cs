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
internal sealed class CON102Analyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CON102",
        title: "Sync-over-async inside [Property] method",
        messageFormat: "'{0}' blocks the thread inside a [Property] method; make the method async instead",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Blocking on async code inside a [Property] method can cause deadlocks. Return Task and use await instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!HasPropertyAttribute(method, context.SemanticModel))
        {
            return;
        }

        SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
        {
            return;
        }

        foreach (SyntaxNode node in body.DescendantNodes())
        {
            string? pattern = GetSyncOverAsyncPattern(node, context.SemanticModel);
            if (pattern is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation(), pattern));
            }
        }
    }

    internal static string? GetSyncOverAsyncPattern(SyntaxNode node, SemanticModel semanticModel)
    {
        if (node is InvocationExpressionSyntax getResultCall &&
            getResultCall.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "GetResult" } getResultAccess &&
            getResultAccess.Expression is InvocationExpressionSyntax getAwaiterCall &&
            getAwaiterCall.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "GetAwaiter" })
        {
            return ".GetAwaiter().GetResult()";
        }

        if (node is InvocationExpressionSyntax waitCall &&
            waitCall.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Wait" } waitAccess &&
            IsTaskType(semanticModel.GetTypeInfo(waitAccess.Expression).Type))
        {
            return ".Wait()";
        }

        // MemberAccess must be checked after invocations to avoid matching GetResult as .Result
        return (node is MemberAccessExpressionSyntax { Name.Identifier.Text: "Result" } resultAccess &&
            IsTaskType(semanticModel.GetTypeInfo(resultAccess.Expression).Type))
            ? ".Result"
            : null;
    }

    internal static bool IsTaskType(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        string fullName = type.ToDisplayString();
        return
            fullName.StartsWith("System.Threading.Tasks.Task", System.StringComparison.Ordinal) ||
            fullName == "System.Threading.Tasks.ValueTask" ||
            fullName.StartsWith("System.Threading.Tasks.ValueTask<", System.StringComparison.Ordinal);
    }

    private static bool HasPropertyAttribute(MethodDeclarationSyntax method, SemanticModel model) =>
        PropertyAttributeHelper.HasPropertyAttribute(method, model);
}
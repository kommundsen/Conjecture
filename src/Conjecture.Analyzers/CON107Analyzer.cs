// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CON107Analyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CON107",
        title: "Non-deterministic operation inside a [Property] method",
        messageFormat: "'{0}' is non-deterministic and breaks reproducibility in [Property] methods; inject randomness via a strategy parameter instead",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Non-deterministic operations break reproducibility in property-based tests.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        string type = memberAccess.Expression.ToString();
        string member = memberAccess.Name.Identifier.Text;

        if (type != "Guid" || member != "NewGuid")
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

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), "Guid.NewGuid()"));
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        ObjectCreationExpressionSyntax creation = (ObjectCreationExpressionSyntax)context.Node;

        if (creation.Type.ToString() != "Random")
        {
            return;
        }

        MethodDeclarationSyntax? method = creation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return;
        }

        if (!PropertyAttributeHelper.HasPropertyAttribute(method, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation(), "new Random()"));
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        MemberAccessExpressionSyntax memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Skip if this member access is the callee of an invocation — handled by AnalyzeInvocation
        if (memberAccess.Parent is InvocationExpressionSyntax)
        {
            return;
        }

        string? display = TryGetNonDeterministicMemberDisplay(memberAccess);
        if (display is null)
        {
            return;
        }

        MethodDeclarationSyntax? method = memberAccess.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return;
        }

        if (!PropertyAttributeHelper.HasPropertyAttribute(method, context.SemanticModel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation(), display));
    }

    private static string? TryGetNonDeterministicMemberDisplay(MemberAccessExpressionSyntax memberAccess)
    {
        string type = memberAccess.Expression.ToString();
        string member = memberAccess.Name.Identifier.Text;

        return (type, member) switch
        {
            ("Random", "Shared") => "Random.Shared",
            ("DateTime", "Now") => "DateTime.Now",
            ("DateTime", "UtcNow") => "DateTime.UtcNow",
            ("DateTimeOffset", "Now") => "DateTimeOffset.Now",
            ("DateTimeOffset", "UtcNow") => "DateTimeOffset.UtcNow",
            ("Environment", "TickCount") => "Environment.TickCount",
            ("Environment", "TickCount64") => "Environment.TickCount64",
            _ => null,
        };
    }
}
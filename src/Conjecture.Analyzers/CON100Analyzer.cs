using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CON100Analyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CON100",
        title: "Assertion inside [Property] method",
        messageFormat: "Assertion '{0}' used inside a void [Property] method; consider returning bool instead",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Property-based tests should return bool to express properties, not use assertion libraries.");

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

        // Scope narrowly: only flag void-returning methods
        if (method.ReturnType is not PredefinedTypeSyntax predefined ||
            !predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            return;
        }

        if (!HasPropertyAttribute(method, context.SemanticModel))
        {
            return;
        }

        SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
        {
            return;
        }

        foreach (InvocationExpressionSyntax invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string? assertionName = GetAssertionName(invocation);
            if (assertionName is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), assertionName));
                return; // one diagnostic per method
            }
        }
    }

    private static bool HasPropertyAttribute(MethodDeclarationSyntax method, SemanticModel model) =>
        PropertyAttributeHelper.HasPropertyAttribute(method, model);

    private static string? GetAssertionName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        string methodName = memberAccess.Name.Identifier.Text;

        return memberAccess.Expression is IdentifierNameSyntax id && id.Identifier.Text == "Assert"
            ? $"Assert.{methodName}"
            : methodName == "Should" ? "Should()" : null;
    }
}

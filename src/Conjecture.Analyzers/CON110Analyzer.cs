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
internal sealed class CON110Analyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CON110",
        title: "Async [Property] method contains no await",
        messageFormat: "[Property] method '{0}' is declared async but contains no await; remove 'async' or add an awaited call",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "An async [Property] method with no await expression adds unnecessary overhead and may indicate a missing await.");

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
        MethodDeclarationSyntax method = (MethodDeclarationSyntax)context.Node;

        if (!PropertyAttributeHelper.HasPropertyAttribute(method, context.SemanticModel))
        {
            return;
        }

        SyntaxToken asyncKeyword = method.Modifiers.FirstOrDefault(
            static m => m.IsKind(SyntaxKind.AsyncKeyword));

        if (asyncKeyword.IsKind(SyntaxKind.None))
        {
            return;
        }

        bool hasAwait = method.DescendantNodes().OfType<AwaitExpressionSyntax>().Any();
        if (hasAwait)
        {
            return;
        }

        string methodName = method.Identifier.Text;
        context.ReportDiagnostic(Diagnostic.Create(Rule, asyncKeyword.GetLocation(), methodName));
    }
}
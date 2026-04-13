// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CON109Analyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CON109",
        title: "No strategy found for [Property] parameter",
        messageFormat: "No strategy found for parameter '{0}' of type '{1}'; add [Arbitrary] to the type or use [From<TStrategy>]",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "All [Property] parameters must have a resolvable strategy.");

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

        foreach (ParameterSyntax parameter in method.ParameterList.Parameters)
        {
            if (PropertyAttributeHelper.HasFromAttribute(parameter, context.SemanticModel))
            {
                continue;
            }

            TypeSyntax? typeSyntax = parameter.Type;
            if (typeSyntax is null)
            {
                continue;
            }

            ITypeSymbol? typeSymbol = context.SemanticModel.GetTypeInfo(typeSyntax).Type;

            if (typeSymbol is null)
            {
                continue;
            }

            if (typeSymbol.SpecialType != SpecialType.None)
            {
                continue;
            }

            if (PropertyAttributeHelper.HasArbitraryAttribute(typeSymbol))
            {
                continue;
            }

            string paramName = parameter.Identifier.Text;
            string typeName = typeSyntax.ToString();
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, parameter.Identifier.GetLocation(), paramName, typeName));
        }
    }
}
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
internal sealed class CON105Analyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "CON105",
        title: "[Arbitrary] provider exists but [From<T>] not used",
        messageFormat: "Parameter '{0}' of type '{1}' has an [Arbitrary] provider; use [From<{1}Arbitrary>] to opt in",
        category: "Conjecture",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "When a type is decorated with [Arbitrary], its generated provider should be referenced explicitly via [From<T>] on the parameter.");

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

        if (!PropertyAttributeHelper.HasPropertyAttribute(method, context.SemanticModel))
        {
            return;
        }

        foreach (ParameterSyntax param in method.ParameterList.Parameters)
        {
            if (param.Type is null)
            {
                continue;
            }

            if (context.SemanticModel.GetSymbolInfo(param.Type).Symbol is not INamedTypeSymbol typeSymbol)
            {
                continue;
            }

            if (!HasArbitraryAttribute(typeSymbol))
            {
                continue;
            }

            if (HasFromAttribute(param, context.SemanticModel))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                param.GetLocation(),
                param.Identifier.Text,
                typeSymbol.Name));
        }
    }

    private static bool HasArbitraryAttribute(INamedTypeSymbol type)
    {
        foreach (AttributeData attr in type.GetAttributes())
        {
            INamedTypeSymbol? attrClass = attr.AttributeClass;
            if (attrClass?.Name == "ArbitraryAttribute" &&
                attrClass.ContainingNamespace?.ToDisplayString() == "Conjecture.Core")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFromAttribute(ParameterSyntax param, SemanticModel model)
    {
        foreach (AttributeListSyntax attrList in param.AttributeLists)
        {
            foreach (AttributeSyntax attr in attrList.Attributes)
            {
                SymbolInfo info = model.GetSymbolInfo(attr);
                ISymbol? symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                if (symbol?.ContainingType?.MetadataName == "FromAttribute`1")
                {
                    return true;
                }

                // Fallback: name-based when attribute is unresolvable
                string name = attr.Name.ToString();
                if (name.StartsWith("From<") || name == "From")
                {
                    return true;
                }
            }
        }

        return false;
    }
}
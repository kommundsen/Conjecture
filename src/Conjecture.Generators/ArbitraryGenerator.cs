// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Generators;

/// <summary>Incremental source generator that emits <c>{TypeName}Arbitrary</c> implementations for types decorated with <c>[Arbitrary]</c>.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ArbitraryGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<INamedTypeSymbol> types = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Conjecture.Core.ArbitraryAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol);

        context.RegisterSourceOutput(types, static (ctx, symbol) =>
        {
            (TypeModel? model, System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);

            bool hasError = false;
            foreach (Diagnostic d in diagnostics)
            {
                ctx.ReportDiagnostic(d);
                if (d.Severity == DiagnosticSeverity.Error)
                {
                    hasError = true;
                }
            }

            if (hasError)
            {
                return;
            }

            string source = StrategyEmitter.Emit(model!);
            ctx.AddSource(model!.TypeName + ".g", source);
        });
    }
}
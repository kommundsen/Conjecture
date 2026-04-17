// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;

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

        IncrementalValueProvider<ImmutableDictionary<string, string>> registry =
            context.CompilationProvider.Select(static (compilation, _) => ProviderRegistry.Build(compilation));

        // Concrete pipeline: filter out abstract types
        IncrementalValuesProvider<INamedTypeSymbol> concreteTypes = types
            .Where(static symbol => !symbol.IsAbstract);

        IncrementalValuesProvider<(INamedTypeSymbol Symbol, ImmutableDictionary<string, string> Registry)> concreteCombined =
            concreteTypes.Combine(registry);

        context.RegisterSourceOutput(concreteCombined, static (ctx, item) =>
        {
            (INamedTypeSymbol symbol, ImmutableDictionary<string, string> reg) = item;
            (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol, reg);

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

        // Hierarchy pipeline: only abstract types
        IncrementalValuesProvider<INamedTypeSymbol> abstractTypes = types
            .Where(static symbol => symbol.IsAbstract);

        IncrementalValueProvider<ImmutableArray<INamedTypeSymbol>> allArbitrarySymbols = types.Collect();

        IncrementalValuesProvider<(INamedTypeSymbol BaseSymbol, ImmutableArray<INamedTypeSymbol> AllSymbols)> hierarchyCombined =
            abstractTypes.Combine(allArbitrarySymbols);

        context.RegisterSourceOutput(hierarchyCombined, static (ctx, item) =>
        {
            (INamedTypeSymbol baseSymbol, ImmutableArray<INamedTypeSymbol> allSymbols) = item;
            (HierarchyTypeModel? model, ImmutableArray<Diagnostic> diagnostics) = HierarchyTypeModelExtractor.Extract(baseSymbol, allSymbols);

            bool hasHierarchyError = false;
            foreach (Diagnostic d in diagnostics)
            {
                ctx.ReportDiagnostic(d);
                if (d.Severity == DiagnosticSeverity.Error)
                {
                    hasHierarchyError = true;
                }
            }

            if (hasHierarchyError || model is null)
            {
                return;
            }

            string source = HierarchyStrategyEmitter.Emit(model);
            ctx.AddSource(model.TypeName + ".g", source);
        });
    }
}

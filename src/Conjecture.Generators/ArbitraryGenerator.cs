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
            (TypeModel? model, Diagnostic? diagnostic) = TypeModelExtractor.Extract(symbol);

            if (diagnostic is not null)
            {
                ctx.ReportDiagnostic(diagnostic);
                return;
            }

            if (model is null)
            {
                return;
            }

            string source = StrategyEmitter.Emit(model);
            ctx.AddSource(model.TypeName + ".g", source);
        });
    }
}

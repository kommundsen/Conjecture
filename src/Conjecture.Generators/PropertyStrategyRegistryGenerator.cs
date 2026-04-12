// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Generators;

/// <summary>Incremental source generator that emits an AOT-safe <c>ConjectureStrategyRegistry.g.cs</c> for <c>[Property]</c> methods whose parameter types have a known <c>IStrategyProvider&lt;T&gt;</c>.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class PropertyStrategyRegistryGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<IMethodSymbol> propertyMethods = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsPropertyMethodCandidate(node),
                transform: static (ctx, _) => TryGetPropertyMethodSymbol(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        IncrementalValueProvider<ImmutableDictionary<string, string>> registry =
            context.CompilationProvider.Select(static (compilation, _) => ProviderRegistry.Build(compilation));

        IncrementalValuesProvider<(string TypeFqn, string ProviderFqn)> pairs =
            propertyMethods
                .Combine(registry)
                .SelectMany(static (item, _) =>
                {
                    IMethodSymbol method = item.Left;
                    ImmutableDictionary<string, string> reg = item.Right;
                    List<(string, string)> result = [];
                    foreach (IParameterSymbol param in method.Parameters)
                    {
                        string typeFqn = param.Type.ToDisplayString(TypeModelExtractor.TypeNameFormat);
                        if (reg.TryGetValue(typeFqn, out string? providerFqn))
                        {
                            result.Add((typeFqn, providerFqn));
                        }
                    }

                    return result;
                });

        IncrementalValueProvider<ImmutableArray<(string TypeFqn, string ProviderFqn)>> collected =
            pairs.Collect();

        context.RegisterSourceOutput(collected, static (ctx, allPairs) =>
        {
            if (allPairs.IsEmpty)
            {
                return;
            }

            // Deduplicate by type FQN
            Dictionary<string, string> unique = new(StringComparer.Ordinal);
            foreach ((string typeFqn, string providerFqn) in allPairs)
            {
                if (!unique.ContainsKey(typeFqn))
                {
                    unique[typeFqn] = providerFqn;
                }
            }

            string source = EmitRegistry(unique);
            ctx.AddSource("ConjectureStrategyRegistry.g.cs", source);
        });
    }

    private static bool IsPropertyMethodCandidate(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax method || method.AttributeLists.Count == 0)
        {
            return false;
        }

        foreach (AttributeListSyntax attrList in method.AttributeLists)
        {
            foreach (AttributeSyntax attr in attrList.Attributes)
            {
                string name = attr.Name.ToString();
                if (name == "Property" || name == "PropertyAttribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IMethodSymbol? TryGetPropertyMethodSymbol(GeneratorSyntaxContext ctx)
    {
        INamedTypeSymbol? markerInterface =
            ctx.SemanticModel.Compilation.GetTypeByMetadataName("Conjecture.Core.IPropertyTest");

        MethodDeclarationSyntax method = (MethodDeclarationSyntax)ctx.Node;
        foreach (AttributeListSyntax attrList in method.AttributeLists)
        {
            foreach (AttributeSyntax attr in attrList.Attributes)
            {
                SymbolInfo info = ctx.SemanticModel.GetSymbolInfo(attr);
                ISymbol? sym = info.Symbol;
                if (sym is null && info.CandidateSymbols.Length > 0)
                {
                    sym = info.CandidateSymbols[0];
                }

                INamedTypeSymbol? attrType = sym?.ContainingType;
                if (attrType is null)
                {
                    continue;
                }

                if (markerInterface is null)
                {
                    if (attrType.Name == "PropertyAttribute")
                    {
                        return ctx.SemanticModel.GetDeclaredSymbol(method) as IMethodSymbol;
                    }

                    continue;
                }

                foreach (INamedTypeSymbol iface in attrType.AllInterfaces)
                {
                    if (SymbolEqualityComparer.Default.Equals(iface, markerInterface))
                    {
                        return ctx.SemanticModel.GetDeclaredSymbol(method) as IMethodSymbol;
                    }
                }
            }
        }

        return null;
    }

    private static string EmitRegistry(Dictionary<string, string> pairs)
    {
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.");
        sb.AppendLine();
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine("namespace Conjecture.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class ConjectureStrategyRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Register()");
        sb.AppendLine("    {");
        sb.AppendLine("        global::Conjecture.Core.ConjectureStrategyRegistrar.Register(Resolve);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static object? Resolve(global::System.Type type)");
        sb.AppendLine("    {");
        foreach (KeyValuePair<string, string> pair in pairs)
        {
            sb.AppendLine($"        if (type == typeof(global::{pair.Key}))");
            sb.AppendLine($"            return ((global::Conjecture.Core.IStrategyProvider<global::{pair.Key}>)new global::{pair.Value}()).Create();");
        }

        sb.AppendLine("        return null;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
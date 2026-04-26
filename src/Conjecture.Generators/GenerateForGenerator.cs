// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Conjecture.Generators;

/// <summary>Incremental source generator that emits <c>GenerateForRegistry.g.cs</c> — a <c>[ModuleInitializer]</c> that registers <c>IStrategyProvider</c> factories for all <c>[Arbitrary]</c> types, enabling <c>Generate.For&lt;T&gt;()</c>.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class GenerateForGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<ImmutableDictionary<string, string>> registry =
            context.CompilationProvider.Select(static (compilation, _) => ProviderRegistry.Build(compilation));

        RegisterCallSiteDiagnosticPipeline(context, registry);

        IncrementalValuesProvider<INamedTypeSymbol> types = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Conjecture.Core.ArbitraryAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol)
            .Where(static symbol => !symbol.IsAbstract && !ImplementsStrategyProvider(symbol));

        IncrementalValuesProvider<(INamedTypeSymbol Symbol, ImmutableDictionary<string, string> Registry)> combined =
            types.Combine(registry);

        IncrementalValueProvider<ImmutableArray<(INamedTypeSymbol Symbol, ImmutableDictionary<string, string> Registry)>> collected =
            combined.Collect();

        context.RegisterSourceOutput(collected, static (ctx, items) =>
        {
            List<(string TypeFqn, string ProviderFqn)> entries = [];
            List<(TypeModel Model, string TypeFqn)> generatedModels = [];

            foreach ((INamedTypeSymbol symbol, ImmutableDictionary<string, string> reg) in items)
            {
                string typeFqn = symbol.ToDisplayString(TypeModelExtractor.TypeNameFormat);
                if (reg.TryGetValue(typeFqn, out string? knownProvider))
                {
                    entries.Add((typeFqn, knownProvider));
                }
                else
                {
                    (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol, reg);
                    bool hasError = false;
                    foreach (Diagnostic d in diagnostics)
                    {
                        if (d.Severity == DiagnosticSeverity.Error)
                        {
                            hasError = true;
                        }
                    }

                    if (!hasError && model is not null)
                    {
                        entries.Add((typeFqn, typeFqn + "ArbitraryFactory"));
                        generatedModels.Add((model, typeFqn));
                    }
                }
            }

            if (entries.Count == 0)
            {
                return;
            }

            string registrySource = EmitRegistry(entries);
            ctx.AddSource("GenerateForRegistry.g.cs", registrySource);

            foreach ((TypeModel model, string _) in generatedModels)
            {
                string arbitrarySource = EmitArbitraryWithOverrides(model);
                ctx.AddSource(model.TypeName + "Arbitrary.g.cs", arbitrarySource);
            }
        });
    }

    private static void RegisterCallSiteDiagnosticPipeline(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableDictionary<string, string>> registryProvider)
    {
        IncrementalValuesProvider<InvocationExpressionSyntax> forCallSites =
            context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax invocation
                    && IsForCallSite(invocation),
                transform: static (ctx, _) => (InvocationExpressionSyntax)ctx.Node);

        IncrementalValuesProvider<(ITypeSymbol? TypeArg, Location Location, Compilation Compilation)> resolvedCalls =
            forCallSites.Combine(context.CompilationProvider)
                .Select(static (pair, _) => ResolveForTypeArg(pair.Left, pair.Right));

        IncrementalValuesProvider<((ITypeSymbol? TypeArg, Location Location, Compilation Compilation) Left, ImmutableDictionary<string, string> Right)> withRegistry =
            resolvedCalls.Combine(registryProvider);

        context.RegisterSourceOutput(withRegistry, static (ctx, pair) =>
        {
            (ITypeSymbol? typeArg, Location location, Compilation compilation) = pair.Left;
            ImmutableDictionary<string, string> registry = pair.Right;

            if (typeArg is null)
            {
                return;
            }

            string typeName = typeArg.ToDisplayString(TypeModelExtractor.TypeNameFormat);

            if (typeArg.TypeKind == TypeKind.Interface)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.Con310, location, typeName));
                return;
            }

            if (typeArg.IsAbstract)
            {
                if (!HasArbitraryConcreteSubtype(typeArg, compilation))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.Con311, location, typeName));
                }

                return;
            }

            if (!registry.ContainsKey(typeName) && !SymbolHelpers.HasArbitraryAttribute((INamedTypeSymbol)typeArg))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.Con312, location, typeName));
            }
        });
    }

    private static bool IsForCallSite(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (memberAccess.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        if (genericName.Identifier.Text != "For")
        {
            return false;
        }

        if (genericName.TypeArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments.Count != 0)
        {
            return false;
        }

        string receiverName = memberAccess.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax nested => nested.Name.Identifier.Text,
            _ => string.Empty,
        };

        return receiverName == "Generate";
    }

    private static (ITypeSymbol? TypeArg, Location Location, Compilation Compilation) ResolveForTypeArg(
        InvocationExpressionSyntax invocation,
        Compilation compilation)
    {
        SemanticModel semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Name is not GenericNameSyntax genericName ||
            genericName.TypeArgumentList.Arguments.Count == 0)
        {
            return (null, invocation.GetLocation(), compilation);
        }

        TypeSyntax typeArgSyntax = genericName.TypeArgumentList.Arguments[0];
        ITypeSymbol? typeArg = semanticModel.GetTypeInfo(typeArgSyntax).Type;

        if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
            method.ContainingType.ToDisplayString() != "Conjecture.Core.Generate")
        {
            return (null, invocation.GetLocation(), compilation);
        }

        return (typeArg, invocation.GetLocation(), compilation);
    }

    private static bool HasArbitraryConcreteSubtype(ITypeSymbol abstractType, Compilation compilation)
    {
        if (SearchNamespaceForArbitrarySubtype(compilation.Assembly.GlobalNamespace, abstractType))
        {
            return true;
        }

        foreach (MetadataReference reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol referencedAssembly &&
                SearchNamespaceForArbitrarySubtype(referencedAssembly.GlobalNamespace, abstractType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SearchNamespaceForArbitrarySubtype(INamespaceSymbol ns, ITypeSymbol abstractType)
    {
        foreach (INamedTypeSymbol type in ns.GetTypeMembers())
        {
            if (!type.IsAbstract && InheritsFromClass(type, abstractType) && SymbolHelpers.HasArbitraryAttribute(type))
            {
                return true;
            }

            if (SearchTypeForArbitrarySubtype(type, abstractType))
            {
                return true;
            }
        }

        foreach (INamespaceSymbol subNs in ns.GetNamespaceMembers())
        {
            if (SearchNamespaceForArbitrarySubtype(subNs, abstractType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SearchTypeForArbitrarySubtype(INamedTypeSymbol containingType, ITypeSymbol abstractType)
    {
        foreach (INamedTypeSymbol nested in containingType.GetTypeMembers())
        {
            if (!nested.IsAbstract && InheritsFromClass(nested, abstractType) && SymbolHelpers.HasArbitraryAttribute(nested))
            {
                return true;
            }

            if (SearchTypeForArbitrarySubtype(nested, abstractType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InheritsFromClass(ITypeSymbol type, ITypeSymbol baseType)
    {
        ITypeSymbol? current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static string EmitArbitraryWithOverrides(TypeModel model)
    {
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine("namespace " + model.Namespace + ";");
            sb.AppendLine();
        }

        string fqn = model.FullyQualifiedName;
        string className = model.TypeName + "ArbitraryFactory";

        sb.AppendLine("internal sealed class " + className + " : global::Conjecture.Core.IStrategyProvider<" + fqn + ">");
        sb.AppendLine("{");

        bool hasMembers = !model.Members.IsEmpty && model.ConstructionMode != ConstructionMode.PartialConstructor;

        if (hasMembers)
        {
            for (int i = 0; i < model.Members.Length; i++)
            {
                MemberModel member = model.Members[i];
                string stratType = StrategyEmitter.ResolveStrategyType(member);
                string genExpr = StrategyEmitter.ResolveGenExpr(member);
                sb.AppendLine("    private static readonly global::Conjecture.Core.Strategy<" + stratType + "> _ov" + i + " = " + genExpr + ";");
            }

            sb.AppendLine();
        }

        // Emit Create() so the class implements IStrategyProvider<T> standalone
        if (model.Members.IsEmpty)
        {
            sb.AppendLine("    public global::Conjecture.Core.Strategy<" + fqn + "> Create() =>");
            sb.AppendLine("        global::Conjecture.Core.Generate.Compose<" + fqn + ">(_ => new " + fqn + "());");
        }
        else if (model.ConstructionMode == ConstructionMode.ObjectInitializer)
        {
            sb.AppendLine("    public global::Conjecture.Core.Strategy<" + fqn + "> Create() =>");
            sb.AppendLine("        global::Conjecture.Core.Generate.Compose<" + fqn + ">(ctx => new " + fqn + " {");

            for (int i = 0; i < model.Members.Length; i++)
            {
                MemberModel member = model.Members[i];
                bool isLast = i == model.Members.Length - 1;
                string suffix = isLast ? " });" : ",";
                sb.AppendLine("            " + member.Name + " = ctx.Generate(_ov" + i + ")" + suffix);
            }

            sb.AppendLine();
            sb.AppendLine("    public global::Conjecture.Core.Strategy<" + fqn + "> CreateWithOverrides(global::Conjecture.Core.ForConfiguration<" + fqn + "> cfg) =>");
            sb.AppendLine("        global::Conjecture.Core.Generate.Compose<" + fqn + ">(ctx => new " + fqn + " {");

            for (int i = 0; i < model.Members.Length; i++)
            {
                MemberModel member = model.Members[i];
                bool isLast = i == model.Members.Length - 1;
                string suffix = isLast ? " });" : ",";
                string tryGet = "cfg.TryGet<" + StrategyEmitter.ResolveStrategyType(member) + ">(\"" + member.Name + "\") ?? _ov" + i;
                sb.AppendLine("            " + member.Name + " = ctx.Generate(" + tryGet + ")" + suffix);
            }
        }
        else
        {
            sb.AppendLine("    public global::Conjecture.Core.Strategy<" + fqn + "> Create() =>");
            sb.AppendLine("        global::Conjecture.Core.Generate.Compose<" + fqn + ">(ctx => new " + fqn + "(");

            for (int i = 0; i < model.Members.Length; i++)
            {
                bool isLast = i == model.Members.Length - 1;
                string suffix = isLast ? "));" : ",";
                sb.AppendLine("            ctx.Generate(_ov" + i + ")" + suffix);
            }

            sb.AppendLine();
            sb.AppendLine("    public global::Conjecture.Core.Strategy<" + fqn + "> CreateWithOverrides(global::Conjecture.Core.ForConfiguration<" + fqn + "> cfg) =>");
            sb.AppendLine("        global::Conjecture.Core.Generate.Compose<" + fqn + ">(ctx => new " + fqn + "(");

            for (int i = 0; i < model.Members.Length; i++)
            {
                MemberModel member = model.Members[i];
                bool isLast = i == model.Members.Length - 1;
                string suffix = isLast ? "));" : ",";
                string tryGet = "cfg.TryGet<" + StrategyEmitter.ResolveStrategyType(member) + ">(\"" + member.Name + "\") ?? _ov" + i;
                sb.AppendLine("            ctx.Generate(" + tryGet + ")" + suffix);
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool ImplementsStrategyProvider(INamedTypeSymbol symbol)
    {
        foreach (INamedTypeSymbol iface in symbol.AllInterfaces)
        {
            if (iface.Name == "IStrategyProvider" &&
                iface.ContainingNamespace is { Name: "Core", ContainingNamespace.Name: "Conjecture" } ns &&
                ns.ContainingNamespace.ContainingNamespace.IsGlobalNamespace)
            {
                return true;
            }
        }

        return false;
    }

    private static string EmitRegistry(List<(string TypeFqn, string ProviderFqn)> entries)
    {
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.");
        sb.AppendLine();
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine("namespace Conjecture.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class GenerateForRegistryInitializer");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Register()");
        sb.AppendLine("    {");
        foreach ((string typeFqn, string providerFqn) in entries)
        {
            sb.AppendLine($"        global::Conjecture.Core.GenerateForRegistry.Register(");
            sb.AppendLine($"            typeof(global::{typeFqn}),");
            sb.AppendLine($"            static () => new global::{providerFqn}());");
            sb.AppendLine($"        global::Conjecture.Core.GenerateForRegistry.RegisterOverride(");
            sb.AppendLine($"            typeof(global::{typeFqn}),");
            sb.AppendLine($"            static cfg => (object)new global::{providerFqn}().CreateWithOverrides((global::Conjecture.Core.ForConfiguration<global::{typeFqn}>)cfg));");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
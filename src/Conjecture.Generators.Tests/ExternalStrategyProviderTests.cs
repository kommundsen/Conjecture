// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

/// <summary>
/// Tests that records/classes with members whose types are covered by an external
/// <c>IStrategyProvider&lt;T&gt;</c> (e.g. <c>TimeProvider</c> → <c>TimeProviderArbitrary</c>)
/// are classified and emitted correctly, enabling AOT-safe code generation.
/// </summary>
public sealed class ExternalStrategyProviderTests
{
    [Fact]
    public void Extract_MemberWithRegistryEntry_ClassifiesAsExternalStrategyProvider()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            "namespace MyApp; public partial record Request(System.TimeProvider Clock);",
            "MyApp.Request");

        Dictionary<string, string> registry = new()
        {
            ["System.TimeProvider"] = "Conjecture.Time.TimeProviderArbitrary",
        };

        (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol, registry);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(model);
        MemberModel member = Assert.Single(model.Members);
        Assert.Equal(MemberGenerationKind.ExternalStrategyProvider, member.Kind);
        Assert.Equal("Conjecture.Time.TimeProviderArbitrary", member.AuxiliaryTypeName);
    }

    [Fact]
    public void Extract_MemberWithNoRegistryEntry_ClassifiesAsUnsupported()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            "namespace MyApp; public partial record Request(System.TimeProvider Clock);",
            "MyApp.Request");

        (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);

        Assert.NotNull(model);
        Assert.Equal(MemberGenerationKind.Unsupported, model.Members[0].Kind);
    }

    [Fact]
    public void Generator_RecordWithTimeProviderMember_EmitsProviderCallInGeneratedCode()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Request(System.TimeProvider Clock);",
            "Request.g.cs");

        Assert.Contains("(global::Conjecture.Core.IStrategyProvider<global::System.TimeProvider>)new global::Conjecture.Time.TimeProviderArbitrary()).Create()", text);
    }

    [Fact]
    public void Generator_RecordWithTimeProviderMember_OutputCompilationHasNoErrors()
    {
        (_, Compilation output, _) = RunGenerator(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Request(System.TimeProvider Clock);");

        IEnumerable<Diagnostic> errors = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    [Fact]
    public void ProviderRegistry_Build_FindsTimeProviderArbitrary()
    {
        CSharpCompilation compilation = CreateCompilation("", includeTime: true);

        ImmutableDictionary<string, string> registry = ProviderRegistry.Build(compilation);

        Assert.True(registry.ContainsKey("System.TimeProvider"));
        Assert.Equal("Conjecture.Time.TimeProviderArbitrary", registry["System.TimeProvider"]);
    }

    private static string GetGeneratedText(string source, string fileName)
    {
        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);
        SyntaxTree? tree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tree);
        return tree.GetText().ToString();
    }

    private static (ImmutableArray<SyntaxTree> GeneratedTrees, Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics) RunGenerator(string source)
    {
        CSharpCompilation inputCompilation = CreateCompilation(source, includeTime: true);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ArbitraryGenerator());
        GeneratorDriverRunResult result = driver.RunGenerators(inputCompilation).GetRunResult();
        Compilation outputCompilation = inputCompilation.AddSyntaxTrees(result.GeneratedTrees);
        return (result.GeneratedTrees, outputCompilation, result.Diagnostics);
    }

    private static CSharpCompilation CreateCompilation(string source, bool includeTime = false)
    {
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(typeof(Conjecture.Core.ArbitraryAttribute).Assembly.Location),
        ];

        if (includeTime)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(Conjecture.Time.TimeProviderArbitrary).Assembly.Location));
        }

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: source.Length > 0 ? [CSharpSyntaxTree.ParseText(source)] : [],
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static INamedTypeSymbol CompileAndGetSymbol(string source, string metadataName)
    {
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        INamedTypeSymbol? symbol = compilation.GetTypeByMetadataName(metadataName);
        Assert.NotNull(symbol);
        return symbol;
    }
}

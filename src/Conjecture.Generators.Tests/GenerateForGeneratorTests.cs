// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class GenerateForGeneratorTests
{
    [Fact]
    public void Record_WithPrimitiveFields_EmitsGenerateForRegistryFile()
    {
        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Order(int Id, string Name);");

        SyntaxTree? tree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith("GenerateForRegistry.g.cs", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tree);
    }

    [Fact]
    public void Record_WithPrimitiveFields_EmittedCodeContainsModuleInitializer()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Order(int Id, string Name);",
            "GenerateForRegistry.g.cs");

        Assert.Contains("[ModuleInitializer]", text);
    }

    [Fact]
    public void Record_WithPrimitiveFields_EmittedCodeRegistersProviderForRecordType()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Order(int Id, string Name);",
            "GenerateForRegistry.g.cs");

        Assert.Contains("typeof(global::MyApp.Order)", text);
    }

    [Fact]
    public void Record_WithPrimitiveFields_EmittedCodeRegistersArbitraryFactory()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Order(int Id, string Name);",
            "GenerateForRegistry.g.cs");

        Assert.Contains("OrderArbitrary", text);
    }

    [Fact]
    public void Struct_WithInitProperties_EmitsRegistryEntry()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial struct Point { public int X { get; init; } public int Y { get; init; } }",
            "GenerateForRegistry.g.cs");

        Assert.Contains("typeof(global::MyApp.Point)", text);
        Assert.Contains("PointArbitrary", text);
    }

    [Fact]
    public void Class_WithConstructorParameters_EmitsRegistryEntry()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial class Box { public Box(int Width, int Height) {} }",
            "GenerateForRegistry.g.cs");

        Assert.Contains("typeof(global::MyApp.Box)", text);
        Assert.Contains("BoxArbitrary", text);
    }

    [Fact]
    public void TwoArbitraryTypes_BothEntriesPresentInRegistry()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Order(int Id); [Arbitrary] public partial record Product(string Name);",
            "GenerateForRegistry.g.cs");

        Assert.Contains("typeof(global::MyApp.Order)", text);
        Assert.Contains("typeof(global::MyApp.Product)", text);
    }

    [Fact]
    public void TypeWithoutArbitraryAttribute_EmitsNoRegistryFile()
    {
        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(
            "namespace MyApp; public class Plain { public int X { get; set; } }");

        SyntaxTree? tree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith("GenerateForRegistry.g.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Null(tree);
    }

    [Fact]
    public void Record_WithPrimitiveFields_GeneratedCodeCompilesWithoutErrors()
    {
        // Include a hand-written OrderArbitrary so the GenerateForRegistry.g.cs reference compiles independently of ArbitraryGenerator.
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Order(int Id, string Name);
            public sealed class OrderArbitrary : global::Conjecture.Core.IStrategyProvider<Order>
            {
                public global::Conjecture.Core.Strategy<Order> Create() =>
                    global::Conjecture.Core.Strategy.Compose<Order>(ctx => new Order(0, ""));
            }
            """;
        (_, Compilation output, _) = RunGenerator(source);

        ImmutableArray<Diagnostic> errors = output.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        Assert.Empty(errors);
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
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        CSharpCompilation inputCompilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(Conjecture.Core.ArbitraryAttribute).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new GenerateForGenerator());
        GeneratorDriverRunResult result = driver.RunGenerators(inputCompilation).GetRunResult();

        Compilation outputCompilation = inputCompilation.AddSyntaxTrees(result.GeneratedTrees);
        return (result.GeneratedTrees, outputCompilation, result.Diagnostics);
    }
}
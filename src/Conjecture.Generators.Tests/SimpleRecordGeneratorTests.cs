// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class SimpleRecordGeneratorTests
{
    [Fact]
    public void ArbitraryRecord_GeneratesFileNamedTypeNameG()
    {
        (ImmutableArray<SyntaxTree> trees, _) = RunGenerator(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Point(int X, int Y);");

        SyntaxTree? generated = trees.FirstOrDefault(t => t.FilePath.EndsWith("Point.g.cs", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(generated);
    }

    [Fact]
    public void ArbitraryRecord_GeneratedClassIsInternalSealedNamedTypeNameArbitrary()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Point(int X, int Y);",
            "Point.g.cs");

        Assert.Contains("internal sealed class PointArbitrary", text);
    }

    [Fact]
    public void ArbitraryRecord_GeneratedClassImplementsIStrategyProviderOfT()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Point(int X, int Y);",
            "Point.g.cs");

        Assert.Contains("IStrategyProvider<", text);
    }

    [Fact]
    public void ArbitraryRecord_GeneratedCreateUsesStrategiesCompose()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Point(int X, int Y);",
            "Point.g.cs");

        Assert.Contains("Generate.Compose<", text);
    }

    [Fact]
    public void ArbitraryRecord_GeneratedCreateUsesGenIntegersForIntParams()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Point(int X, int Y);",
            "Point.g.cs");

        Assert.Contains("Generate.Integers<int>()", text);
    }

    [Fact]
    public void ArbitraryRecord_OutputCompilationHasNoErrors()
    {
        (_, Compilation output) = RunGenerator(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Point(int X, int Y);");

        IEnumerable<Diagnostic> errors = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    private static string GetGeneratedText(string source, string fileName)
    {
        (ImmutableArray<SyntaxTree> trees, _) = RunGenerator(source);
        SyntaxTree? tree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tree);
        return tree.GetText().ToString();
    }

    private static (ImmutableArray<SyntaxTree> GeneratedTrees, Compilation Output) RunGenerator(string source)
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

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ArbitraryGenerator());
        GeneratorDriverRunResult result = driver.RunGenerators(inputCompilation).GetRunResult();

        Compilation outputCompilation = inputCompilation.AddSyntaxTrees(result.GeneratedTrees);
        return (result.GeneratedTrees, outputCompilation);
    }
}
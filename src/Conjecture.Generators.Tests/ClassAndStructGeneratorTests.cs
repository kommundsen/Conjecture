// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class ClassAndStructGeneratorTests
{
    [Fact]
    public void Class_WithConstructor_GeneratesConstructorBasedStrategy()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial class Box { public Box(int Width, int Height) {} }",
            "Box.g.cs");

        Assert.Contains("new MyApp.Box(", text);
        Assert.Contains("ctx.Generate(", text);
    }

    [Fact]
    public void Struct_WithInitProperties_GeneratesObjectInitializerStrategy()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial struct Point { public int X { get; init; } public int Y { get; init; } }",
            "Point.g.cs");

        Assert.Contains("new MyApp.Point {", text);
        Assert.Contains("X = ctx.Generate(", text);
        Assert.Contains("Y = ctx.Generate(", text);
    }

    [Fact]
    public void Class_WithMultipleConstructors_UsesConstructorWithMostParameters()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial class Box { public Box(int W) {} public Box(int W, int H) {} }",
            "Box.g.cs");

        Assert.Equal(2, CountOccurrences(text, "ctx.Generate("));
    }

    [Fact]
    public void Class_WithOnlyPrivateConstructor_EmitsCon200Diagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial class Singleton { private Singleton() {} }");

        Assert.Contains(diagnostics, d => d.Id == "CON200");
    }

    private static string GetGeneratedText(string source, string fileName)
    {
        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);
        SyntaxTree? tree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tree);
        return tree.GetText().ToString();
    }

    private static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(string source)
    {
        (_, _, ImmutableArray<Diagnostic> diagnostics) = RunGenerator(source);
        return diagnostics;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
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

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ArbitraryGenerator());
        GeneratorDriverRunResult result = driver.RunGenerators(inputCompilation).GetRunResult();

        Compilation outputCompilation = inputCompilation.AddSyntaxTrees(result.GeneratedTrees);
        return (result.GeneratedTrees, outputCompilation, result.Diagnostics);
    }
}
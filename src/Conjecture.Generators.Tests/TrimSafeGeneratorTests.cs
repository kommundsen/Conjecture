// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class TrimSafeGeneratorTests
{
    private static readonly string[] TrimUnsafePatterns =
    [
        "typeof(",
        ".GetType()",
        "Activator.CreateInstance",
        "MethodInfo",
        "PropertyInfo",
        "FieldInfo",
        "MemberInfo",
        "Assembly.",
        "RuntimeReflectionExtensions",
        "Type.GetType(",
    ];

    [Theory]
    [InlineData(
        "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Point(int X, int Y);",
        "Point.g.cs")]
    [InlineData(
        "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record AllPrims(bool B, int I, long L, byte By, double D, float F, string S);",
        "AllPrims.g.cs")]
    [InlineData(
        "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Wrapper(int? Value);",
        "Wrapper.g.cs")]
    [InlineData(
        "using System.Collections.Generic; using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Container(List<int> Items);",
        "Container.g.cs")]
    [InlineData(
        "using Conjecture.Core; namespace MyApp; public enum Color { Red, Green, Blue } [Arbitrary] public partial record Palette(Color Primary);",
        "Palette.g.cs")]
    [InlineData(
        "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial class Box { public int Width { get; init; } public int Height { get; init; } }",
        "Box.g.cs")]
    public void GeneratedCode_ContainsNoReflectionCalls(string source, string expectedFileName)
    {
        string text = GetGeneratedText(source, expectedFileName);
        AssertNoTrimUnsafePatterns(text);
    }

    [Fact]
    public void GeneratedCode_PrimitiveRecord_UsesOnlyTrimSafeApiPatterns()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Point(int X, int Y);",
            "Point.g.cs");

        Assert.Contains("Strategy.Compose<", text);
        Assert.Contains("ctx.Generate(", text);
        Assert.Contains("Strategy.", text);
    }

    [Fact]
    public void GeneratedCode_AllSupportedShapes_OutputCompilationHasNoErrors()
    {
        string[] sources =
        [
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Point(int X, int Y);",
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial class Box { public int W { get; init; } }",
            "using System.Collections.Generic; using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Container(List<int> Items);",
        ];

        foreach (string source in sources)
        {
            (_, Compilation output) = RunGenerator(source);
            IEnumerable<Diagnostic> errors = output.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error);
            Assert.Empty(errors);
        }
    }

    private static void AssertNoTrimUnsafePatterns(string generatedText)
    {
        foreach (string pattern in TrimUnsafePatterns)
        {
            Assert.DoesNotContain(pattern, generatedText);
        }
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
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Collections.dll")),
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
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class ArbitraryGeneratorHierarchyTests
{
    [Fact]
    public void AbstractBaseWithTwoConcreteSubtypes_GeneratesSourceFileForEachBase()
    {
        string source = """
            using Conjecture.Core;

            namespace MyApp;

            [Arbitrary]
            public abstract partial class Animal { }

            [Arbitrary]
            public partial class Dog : Animal { }

            [Arbitrary]
            public partial class Cat : Animal { }
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        SyntaxTree? animalTree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith("Animal.g.cs", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(animalTree);
    }

    [Fact]
    public void AbstractBaseWithTwoConcreteSubtypes_EmitsStrategyUsingBothSubtypeProviders()
    {
        string source = """
            using Conjecture.Core;

            namespace MyApp;

            [Arbitrary]
            public abstract partial class Animal { }

            [Arbitrary]
            public partial class Dog : Animal { }

            [Arbitrary]
            public partial class Cat : Animal { }
            """;

        string text = GetGeneratedText(source, "Animal.g.cs");

        Assert.Contains("DogArbitrary", text);
        Assert.Contains("CatArbitrary", text);
    }

    [Fact]
    public void ConcreteArbitraryType_StillGeneratesViaExistingPipeline()
    {
        string source = """
            using Conjecture.Core;

            namespace MyApp;

            [Arbitrary]
            public partial class Point { public Point(int X, int Y) { } }
            """;

        string text = GetGeneratedText(source, "Point.g.cs");

        Assert.Contains("new MyApp.Point(", text);
        Assert.Contains("ctx.Generate(", text);
    }

    [Fact]
    public void AbstractBaseWithNoConcreteSubtypes_EmitsCon302Diagnostic()
    {
        string source = """
            using Conjecture.Core;

            namespace MyApp;

            [Arbitrary]
            public abstract partial class Animal { }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "CON302");
    }

    [Fact]
    public void AbstractBaseWithOnlyAbstractSubtype_EmitsCon302Diagnostic()
    {
        string source = """
            using Conjecture.Core;

            namespace MyApp;

            [Arbitrary]
            public abstract partial class Animal { }

            [Arbitrary]
            public abstract partial class Mammal : Animal { }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "CON302");
    }

    [Fact]
    public void MultipleAbstractBases_GeneratesSeparateSourceFilesForEach()
    {
        string source = """
            using Conjecture.Core;

            namespace MyApp;

            [Arbitrary]
            public abstract partial class Shape { }

            [Arbitrary]
            public partial class Circle : Shape { }

            [Arbitrary]
            public abstract partial class Color { }

            [Arbitrary]
            public partial class Red : Color { }
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        SyntaxTree? shapeTree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith("Shape.g.cs", StringComparison.OrdinalIgnoreCase));
        SyntaxTree? colorTree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith("Color.g.cs", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(shapeTree);
        Assert.NotNull(colorTree);
    }

    [Fact]
    public void ConcreteAndAbstractTypes_GeneratesBothConcretePipelineAndHierarchyPipeline()
    {
        string source = """
            using Conjecture.Core;

            namespace MyApp;

            [Arbitrary]
            public partial class Point { public Point(int X, int Y) { } }

            [Arbitrary]
            public abstract partial class Animal { }

            [Arbitrary]
            public partial class Dog : Animal { }
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        SyntaxTree? pointTree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith("Point.g.cs", StringComparison.OrdinalIgnoreCase));
        SyntaxTree? animalTree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith("Animal.g.cs", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(pointTree);
        Assert.NotNull(animalTree);
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
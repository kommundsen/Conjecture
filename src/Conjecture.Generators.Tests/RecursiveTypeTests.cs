// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class RecursiveTypeTests
{
    // --- Behaviour 1: self-referential type emits Strategy.Recursive with default maxDepth: 5 ---

    [Fact]
    public void SelfReferentialRecord_EmitsGenerateRecursiveWrapper()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record TreeNode(int Value, [Arbitrary] TreeNode? Child);
            """;

        string text = GetGeneratedText(source, "TreeNode.g.cs");

        Assert.Contains("Strategy.Recursive<", text);
    }

    [Fact]
    public void SelfReferentialRecord_DefaultMaxDepthIsFive()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record TreeNode(int Value, [Arbitrary] TreeNode? Child);
            """;

        string text = GetGeneratedText(source, "TreeNode.g.cs");

        Assert.Contains("maxDepth: 5", text);
    }

    // --- Behaviour 2: [GenMaxDepth(3)] overrides maxDepth ---

    [Fact]
    public void GenMaxDepthAttribute_OverridesDefaultMaxDepth()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary, GenMaxDepth(3)] public partial record TreeNode(int Value, [Arbitrary] TreeNode? Child);
            """;

        string text = GetGeneratedText(source, "TreeNode.g.cs");

        Assert.Contains("maxDepth: 3", text);
    }

    [Fact]
    public void GenMaxDepthAttribute_DoesNotEmitDefaultMaxDepth()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary, GenMaxDepth(3)] public partial record TreeNode(int Value, [Arbitrary] TreeNode? Child);
            """;

        string text = GetGeneratedText(source, "TreeNode.g.cs");

        Assert.DoesNotContain("maxDepth: 5", text);
    }

    // --- Behaviour 3: mutually recursive types emit CON313 warning ---

    [Fact]
    public void MutuallyRecursiveTypes_EmitsCon313Warning()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Alpha(int X, [Arbitrary] Beta? Next);
            [Arbitrary] public partial record Beta(int Y, [Arbitrary] Alpha? Next);
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "CON313");
    }

    [Fact]
    public void MutuallyRecursiveTypes_Con313IsWarning()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Alpha(int X, [Arbitrary] Beta? Next);
            [Arbitrary] public partial record Beta(int Y, [Arbitrary] Alpha? Next);
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? con313 = diagnostics.FirstOrDefault(d => d.Id == "CON313");

        Assert.NotNull(con313);
        Assert.Equal(DiagnosticSeverity.Warning, con313.Severity);
    }

    [Fact]
    public void MutuallyRecursiveTypes_StillGeneratesOutput()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Alpha(int X, [Arbitrary] Beta? Next);
            [Arbitrary] public partial record Beta(int Y, [Arbitrary] Alpha? Next);
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        Assert.NotEmpty(trees);
    }

    // --- Behaviour 4: non-recursive type does NOT emit Strategy.Recursive ---

    [Fact]
    public void NonRecursiveRecord_DoesNotEmitGenerateRecursive()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Person(string Name, int Age);
            """;

        string text = GetGeneratedText(source, "Person.g.cs");

        Assert.DoesNotContain("Strategy.Recursive", text);
    }

    // --- Behaviour 5: base case passes null for recursive slot ---

    [Fact]
    public void SelfReferentialRecord_BaseCasePassesNullForChildParameter()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record TreeNode(int Value, [Arbitrary] TreeNode? Child);
            """;

        string text = GetGeneratedText(source, "TreeNode.g.cs");

        Assert.Contains("null", text);
    }

    // --- Behaviour 6: self-referential class with object-initializer mode emits valid Strategy.Recursive ---

    [Fact]
    public void SelfReferentialType_WithObjectInitializerMode_EmitsValidCode()
    {
        string source = """
            using Conjecture.Core;

            namespace Tests;

            [Arbitrary]
            public class TreeClass
            {
                public string Name { get; init; } = "";
                public TreeClass? Child { get; init; }
            }
            """;

        string text = GetGeneratedText(source, "TreeClassArbitrary.g.cs");

        Assert.Contains("Strategy.Recursive<", text);
        Assert.DoesNotContain("new global::Tests.TreeClass(", text);
    }

    // --- Behaviour 7: mutual recursion via nullable init property triggers CON313 ---

    [Fact]
    public void MutuallyRecursiveTypes_ViaInitProperty_EmitsCon313Warning()
    {
        string source = """
            using Conjecture.Core;

            namespace Tests;

            [Arbitrary]
            public class NodeA
            {
                public string Value { get; init; } = "";
                public NodeB? Partner { get; init; }
            }

            [Arbitrary]
            public class NodeB
            {
                public int Score { get; init; }
                public NodeA? Back { get; init; }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? con313 = diagnostics.FirstOrDefault(d => d.Id == "CON313");

        Assert.NotNull(con313);
    }

    // --- helpers ---

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
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Collections.dll")),
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
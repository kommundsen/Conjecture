// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class ConstraintAttributeTests
{
    [Fact]
    public void GenRange_OnInt_EmitsBoundedIntegers()
    {
        string text = GetGeneratedText(
            """
            using System.ComponentModel.DataAnnotations;
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record R([GenRange(0, 100)] int Count);
            """,
            "R.g.cs");

        Assert.Contains("Strategy.Integers<int>(0, 100)", text);
    }

    [Fact]
    public void GenRange_OnDecimal_EmitsBoundedDecimals()
    {
        string text = GetGeneratedText(
            """
            using System.ComponentModel.DataAnnotations;
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record R([GenRange(0.01, 999.99)] decimal Total);
            """,
            "R.g.cs");

        Assert.Contains("Strategy.Decimals(0.01m, 999.99m)", text);
    }

    [Fact]
    public void StringLength_DataAnnotation_EmitsBoundedStrings()
    {
        string text = GetGeneratedText(
            """
            using System.ComponentModel.DataAnnotations;
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record R([StringLength(50, MinimumLength = 1)] string Name);
            """,
            "R.g.cs");

        Assert.Contains("Strategy.Strings(minLength: 1, maxLength: 50)", text);
    }

    [Fact]
    public void Required_OnNullableReference_EmitsNonNullableString()
    {
        string text = GetGeneratedText(
            """
            using System.ComponentModel.DataAnnotations;
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record R([Required] string? Customer);
            """,
            "R.g.cs");

        Assert.Contains("Strategy<string>", text);
        Assert.DoesNotContain("Strategy<string?>", text);
        Assert.DoesNotContain("Strategy.Nullable(", text);
    }

    [Fact]
    public void GenRange_WinsOver_RangeDataAnnotation()
    {
        string text = GetGeneratedText(
            """
            using System.ComponentModel.DataAnnotations;
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record R([GenRange(0, 10)] [Range(5, 20)] int Score);
            """,
            "R.g.cs");

        Assert.Contains("Strategy.Integers<int>(0, 10)", text);
    }

    [Fact]
    public void NoConstraintAttribute_EmitsUnboundedPrimitive()
    {
        string text = GetGeneratedText(
            """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record R(int Count);
            """,
            "R.g.cs");

        Assert.Contains("Strategy.Integers<int>()", text);
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
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.ComponentModel.DataAnnotations.dll")),
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
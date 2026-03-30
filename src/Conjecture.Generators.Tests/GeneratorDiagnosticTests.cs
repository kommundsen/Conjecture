using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class GeneratorDiagnosticTests
{
    // --- CON200: no accessible constructor ---

    [Fact]
    public void NoAccessibleConstructor_EmitsCon200()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial class Hidden { private Hidden() { } }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == DiagnosticDescriptors.Con200.Id);
    }

    [Fact]
    public void NoAccessibleConstructor_Con200IsError()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial class Hidden { private Hidden() { } }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? hyp200 = diagnostics.FirstOrDefault(d => d.Id == DiagnosticDescriptors.Con200.Id);

        Assert.NotNull(hyp200);
        Assert.Equal(DiagnosticSeverity.Error, hyp200.Severity);
    }

    [Fact]
    public void NoAccessibleConstructor_NoSourceGenerated()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial class Hidden { private Hidden() { } }
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        Assert.Empty(trees);
    }

    // --- CON201: type not partial ---

    [Fact]
    public void NonPartialType_EmitsCon201()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public class Solid { public Solid(int x) { } }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == DiagnosticDescriptors.Con201.Id);
    }

    [Fact]
    public void NonPartialType_Con201IsError()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public class Solid { public Solid(int x) { } }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? hyp201 = diagnostics.FirstOrDefault(d => d.Id == DiagnosticDescriptors.Con201.Id);

        Assert.NotNull(hyp201);
        Assert.Equal(DiagnosticSeverity.Error, hyp201.Severity);
    }

    [Fact]
    public void NonPartialType_NoSourceGenerated()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public class Solid { public Solid(int x) { } }
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        Assert.Empty(trees);
    }

    // --- CON202: unsupported member type ---

    [Fact]
    public void UnsupportedMemberType_EmitsCon202()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public class Custom {}
            [Arbitrary] public partial record W(Custom Item);
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == DiagnosticDescriptors.Con202.Id);
    }

    [Fact]
    public void UnsupportedMemberType_Con202IsWarning()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public class Custom {}
            [Arbitrary] public partial record W(Custom Item);
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? hyp202 = diagnostics.FirstOrDefault(d => d.Id == DiagnosticDescriptors.Con202.Id);

        Assert.NotNull(hyp202);
        Assert.Equal(DiagnosticSeverity.Warning, hyp202.Severity);
    }

    // --- Valid types produce no diagnostics ---

    [Fact]
    public void ValidRecord_ProducesNoDiagnostics()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Point(int X, int Y);
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidClass_ProducesNoDiagnostics()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial class Box { public Box(int value) { } }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Empty(diagnostics);
    }

    // --- helpers ---

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

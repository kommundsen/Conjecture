using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Conjecture.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers.Tests;

public sealed class CON101Tests
{
    // --- Equality on unbounded integer strategy ---

    [Fact]
    public async Task Integers_EqualityPredicate_EmitsCon101()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Integers<int>().Where(x => x == 42); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON101");
    }

    [Fact]
    public async Task Integers_EqualityPredicate_Con101IsWarning()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Integers<int>().Where(x => x == 42); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? con101 = diagnostics.FirstOrDefault(d => d.Id == "CON101");

        Assert.NotNull(con101);
        Assert.Equal(DiagnosticSeverity.Warning, con101.Severity);
    }

    // --- Boolean equality ---

    [Fact]
    public async Task Booleans_EqualityToTrue_EmitsCon101()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Booleans().Where(b => b == true); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON101");
    }

    // --- False literal predicate ---

    [Fact]
    public async Task Where_FalseLiteralPredicate_EmitsCon101()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Integers<int>().Where(x => false); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON101");
    }

    // --- Complex predicates: no diagnostic ---

    [Fact]
    public async Task Where_ComplexPredicate_NoCon101()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Integers<int>().Where(x => x > 0 && x < 100); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON101");
    }

    // --- Helpers ---

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Numerics.dll")),
            MetadataReference.CreateFromFile(typeof(Conjecture.Core.Generate).Assembly.Location),
        ];
    }

    private static CSharpCompilation CreateCompilation(string source) =>
        CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetReferences(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        var analyzer = new CON101Analyzer();
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}

// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Conjecture.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers.Tests;

public sealed class CON104Tests
{
    // --- Assume.That(false) literal → CON104 ---

    [Fact]
    public async Task AssumeThat_FalseLiteral_EmitsCon104()
    {
        string source = """
            using Conjecture.Core;
            class Tests { void M() { Assume.That(false); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON104");
    }

    [Fact]
    public async Task AssumeThat_FalseLiteral_Con104IsWarning()
    {
        string source = """
            using Conjecture.Core;
            class Tests { void M() { Assume.That(false); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? hyp104 = diagnostics.FirstOrDefault(d => d.Id == "CON104");

        Assert.NotNull(hyp104);
        Assert.Equal(DiagnosticSeverity.Warning, hyp104.Severity);
    }

    // --- Assume.That(true) → no diagnostic ---

    [Fact]
    public async Task AssumeThat_TrueLiteral_NoCon104()
    {
        string source = """
            using Conjecture.Core;
            class Tests { void M() { Assume.That(true); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON104");
    }

    // --- Assume.That(variable) → no diagnostic ---

    [Fact]
    public async Task AssumeThat_VariableArgument_NoCon104()
    {
        string source = """
            using Conjecture.Core;
            class Tests { void M(bool condition) { Assume.That(condition); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON104");
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
            MetadataReference.CreateFromFile(typeof(Conjecture.Core.Assume).Assembly.Location),
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
        var analyzer = new CON104Analyzer();
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
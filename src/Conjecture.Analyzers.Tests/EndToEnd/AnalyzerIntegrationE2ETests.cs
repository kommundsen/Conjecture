// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Conjecture.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers.Tests.EndToEnd;

/// <summary>
/// Integration tests that run multiple analyzers simultaneously.
/// Single-analyzer and code-fix tests live in the per-analyzer test files.
/// </summary>
public sealed class AnalyzerIntegrationE2ETests
{
    // Stub [Property] attribute works for CON100/CON102 (detected by name).
    // Real Conjecture.Core is referenced for CON101/CON103/CON104/CON105.
    private const string Preamble = """
        using System;
        using System.Threading.Tasks;
        using Conjecture.Core;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}
        static class Assert { public static void Equal<T>(T a, T b) {} }

        """;

    // Source that purposely triggers all 6 diagnostics at once.
    private const string AllViolationsSource = """
        [Arbitrary] partial class Person { }

        class Tests {
            // CON100: void [Property] with Assert call
            [Property] public void PropWithAssert(int x) { Assert.Equal(x, x); }

            // CON101: high-rejection .Where() predicate
            void BadStrategy() { var s = Strategy.Integers<int>().Where(x => x == 42); }

            // CON102: sync-over-async inside [Property]
            [Property] public void PropWithBlocking(int x) { Task.Delay(0).GetAwaiter().GetResult(); }

            // CON103: inverted constant bounds
            void InvertedBounds() { var s = Strategy.Integers(10, 5); }

            // CON104: Assume.That(false)
            [Property] public void PropWithFalseAssume(int x) { Assume.That(false); }

            // CON105: [Arbitrary] type param without [From<>]
            [Property] public void PropWithPersonParam(Person p) { }
        }
        """;

    // --- All 6 diagnostics fire on purpose-built code ---

    [Fact]
    public async Task AllViolations_AllSixDiagnosticsFire()
    {
        ImmutableArray<Diagnostic> diagnostics =
            await GetDiagnosticsAsync(Preamble + AllViolationsSource, AllAnalyzers());

        string[] ids = diagnostics.Select(d => d.Id).Distinct().Order().ToArray();
        Assert.Contains("CON100", ids);
        Assert.Contains("CON101", ids);
        Assert.Contains("CON102", ids);
        Assert.Contains("CON103", ids);
        Assert.Contains("CON104", ids);
        Assert.Contains("CON105", ids);
    }

    [Fact]
    public async Task AllViolations_EachDiagnosticFiresExactlyOnce()
    {
        ImmutableArray<Diagnostic> diagnostics =
            await GetDiagnosticsAsync(Preamble + AllViolationsSource, AllAnalyzers());

        Assert.Single(diagnostics, d => d.Id == "CON100");
        Assert.Single(diagnostics, d => d.Id == "CON101");
        Assert.Single(diagnostics, d => d.Id == "CON102");
        Assert.Single(diagnostics, d => d.Id == "CON103");
        Assert.Single(diagnostics, d => d.Id == "CON104");
        Assert.Single(diagnostics, d => d.Id == "CON105");
    }

    // --- No false positives on clean code ---

    [Fact]
    public async Task CleanPropertyTest_ProducesNoDiagnostics()
    {
        string source = Preamble + """
            class Tests {
                [Property] public bool Commutativity(int x, int y) => x + y == y + x;
                [Property] public bool ListBounds(int n)
                {
                    var s = Strategy.Lists(Strategy.Integers<int>(), minSize: 0, maxSize: 10);
                    return true;
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await GetDiagnosticsAsync(source, AllAnalyzers());

        Assert.Empty(diagnostics.Where(d => d.Id.StartsWith("CON")));
    }

    [Fact]
    public async Task ValidGenBounds_ProducesNoDiagnostics()
    {
        string source = Preamble + """
            class Tests {
                void Setup()
                {
                    var a = Strategy.Integers(0, 100);
                    var b = Strategy.Doubles(0.0, 1.0);
                    var c = Strategy.Strings(minLength: 0, maxLength: 20);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await GetDiagnosticsAsync(source, AllAnalyzers());

        Assert.Empty(diagnostics.Where(d => d.Id == "CON103"));
    }

    // --- No interference between analyzers ---

    [Fact]
    public async Task AllAnalyzersRunTogether_DiagnosticCountMatchesSumOfIndividual()
    {
        string source = Preamble + AllViolationsSource;

        ImmutableArray<Diagnostic> combined =
            await GetDiagnosticsAsync(source, AllAnalyzers());

        Assert.Equal(1, combined.Count(d => d.Id == "CON100"));
        Assert.Equal(1, combined.Count(d => d.Id == "CON101"));
        Assert.Equal(1, combined.Count(d => d.Id == "CON102"));
        Assert.Equal(1, combined.Count(d => d.Id == "CON103"));
        Assert.Equal(1, combined.Count(d => d.Id == "CON104"));
        Assert.Equal(1, combined.Count(d => d.Id == "CON105"));
    }

    [Fact]
    public async Task GeneratedCodeAnnotated_AnalyzersSkipGeneratedCode()
    {
        // Simulate a generated file by adding the auto-generated header comment.
        // Analyzers configure GeneratedCodeAnalysisFlags.None so should produce no diagnostics.
        string source = """
            // <auto-generated/>
            using Conjecture.Core;
            class GeneratedTests {
                void M() { var s = Strategy.Integers(10, 5); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics =
            await GetDiagnosticsAsync(source, AllAnalyzers());

        Assert.Empty(diagnostics.Where(d => d.Id.StartsWith("CON")));
    }

    // --- Helpers ---

    private static ImmutableArray<DiagnosticAnalyzer> AllAnalyzers() =>
    [
        new CON100Analyzer(),
        new CON101Analyzer(),
        new CON102Analyzer(),
        new CON103Analyzer(),
        new CON104Analyzer(),
        new CON105Analyzer(),
    ];

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Threading.Tasks.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Numerics.dll")),
            MetadataReference.CreateFromFile(typeof(Conjecture.Core.Strategy).Assembly.Location),
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

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source, ImmutableArray<DiagnosticAnalyzer> analyzers)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        CompilationWithAnalyzers compilationWithAnalyzers =
            compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
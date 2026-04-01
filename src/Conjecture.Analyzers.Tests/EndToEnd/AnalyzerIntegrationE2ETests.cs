// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conjecture.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Conjecture.Analyzers.Tests.EndToEnd;

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
            void BadStrategy() { var s = Generate.Integers<int>().Where(x => x == 42); }

            // CON102: sync-over-async inside [Property]
            [Property] public void PropWithBlocking(int x) { Task.Delay(0).GetAwaiter().GetResult(); }

            // CON103: inverted constant bounds
            void InvertedBounds() { var s = Generate.Integers(10, 5); }

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

    // --- Code-fix for CON103 produces compilable code ---

    [Fact]
    public async Task CON103CodeFix_ProducesCompilableOutput()
    {
        string source = Preamble + """
            class Test { void M() { var s = Generate.Integers(10, 5); } }
            """;

        string? fixed_ = await ApplyCon103FixAsync(source);

        Assert.NotNull(fixed_);
        CSharpCompilation compilation = CreateCompilation(fixed_);
        IEnumerable<Diagnostic> errors =
            compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task CON103CodeFix_SwapsToCorrectBounds()
    {
        string source = Preamble + """
            class Test { void M() { var s = Generate.Integers(10, 5); } }
            """;

        string? fixed_ = await ApplyCon103FixAsync(source);

        Assert.NotNull(fixed_);
        Assert.Contains("Generate.Integers(5, 10)", fixed_);
    }

    // --- Code-fix for CON102 produces compilable code ---

    [Fact]
    public async Task CON102CodeFix_ProducesCompilableOutput()
    {
        string source = Preamble + """
            class Tests {
                [Property] public void PropWithBlocking(int x) { Task.Delay(0).GetAwaiter().GetResult(); }
            }
            """;

        string? fixed_ = await ApplyCon102FixAsync(source);

        Assert.NotNull(fixed_);
        CSharpCompilation compilation = CreateCompilation(fixed_);
        IEnumerable<Diagnostic> errors =
            compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task CON102CodeFix_ConvertsToAsyncAwait()
    {
        string source = Preamble + """
            class Tests {
                [Property] public void PropWithBlocking(int x) { Task.Delay(0).GetAwaiter().GetResult(); }
            }
            """;

        string? fixed_ = await ApplyCon102FixAsync(source);

        Assert.NotNull(fixed_);
        Assert.Contains("async", fixed_);
        Assert.Contains("await", fixed_);
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
                    var s = Generate.Lists(Generate.Integers<int>(), minSize: 0, maxSize: 10);
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
                    var a = Generate.Integers(0, 100);
                    var b = Generate.Doubles(0.0, 1.0);
                    var c = Generate.Strings(minLength: 0, maxLength: 20);
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

        // Each individual analyzer should contribute its expected diagnostic
        int con100 = combined.Count(d => d.Id == "CON100");
        int con101 = combined.Count(d => d.Id == "CON101");
        int con102 = combined.Count(d => d.Id == "CON102");
        int con103 = combined.Count(d => d.Id == "CON103");
        int con104 = combined.Count(d => d.Id == "CON104");
        int con105 = combined.Count(d => d.Id == "CON105");

        Assert.Equal(1, con100);
        Assert.Equal(1, con101);
        Assert.Equal(1, con102);
        Assert.Equal(1, con103);
        Assert.Equal(1, con104);
        Assert.Equal(1, con105);
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
                void M() { var s = Generate.Integers(10, 5); }
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

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source, ImmutableArray<DiagnosticAnalyzer> analyzers)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        CompilationWithAnalyzers compilationWithAnalyzers =
            compilation.WithAnalyzers(analyzers);
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static async Task<string?> ApplyCon103FixAsync(string source) =>
        await ApplyFixAsync(source, "CON103", new CON103Analyzer(), new CON103CodeFix());

    private static async Task<string?> ApplyCon102FixAsync(string source) =>
        await ApplyFixAsync(source, "CON102", new CON102Analyzer(), new CON102CodeFix());

    private static async Task<string?> ApplyFixAsync(
        string source, string diagnosticId,
        DiagnosticAnalyzer analyzer, CodeFixProvider fix)
    {
        using var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        Solution solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId, VersionStamp.Create(), "Test", "Test", LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable),
                metadataReferences: GetReferences()));

        DocumentId documentId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(
            DocumentInfo.Create(documentId, "Test.cs",
                loader: TextLoader.From(
                    TextAndVersion.Create(SourceText.From(source), VersionStamp.Create()))));

        workspace.TryApplyChanges(solution);
        Document document = workspace.CurrentSolution.GetDocument(documentId)!;

        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        CompilationWithAnalyzers cwAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create(analyzer));
        ImmutableArray<Diagnostic> mapped = await cwAnalyzers.GetAnalyzerDiagnosticsAsync();
        Diagnostic? target = mapped.FirstOrDefault(d => d.Id == diagnosticId);
        if (target is null) return null;

        var actions = new List<CodeAction>();
        CodeFixContext context = new(
            document, target,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await fix.RegisterCodeFixesAsync(context);

        if (!actions.Any()) return null;

        ImmutableArray<CodeActionOperation> operations =
            await actions[0].GetOperationsAsync(CancellationToken.None);
        foreach (CodeActionOperation op in operations)
            op.Apply(workspace, CancellationToken.None);

        Document updated = workspace.CurrentSolution.GetDocument(documentId)!;
        SourceText text = await updated.GetTextAsync();
        return text.ToString();
    }
}
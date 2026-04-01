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

namespace Conjecture.Analyzers.Tests;

public sealed class CON103Tests
{
    // --- Integers: inverted bounds ---

    [Fact]
    public async Task Integers_InvertedConstantBounds_EmitsCon103()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Integers(10, 5); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON103");
    }

    [Fact]
    public async Task Integers_InvertedConstantBounds_Con103IsError()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Integers(10, 5); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? hyp103 = diagnostics.FirstOrDefault(d => d.Id == "CON103");

        Assert.NotNull(hyp103);
        Assert.Equal(DiagnosticSeverity.Error, hyp103.Severity);
    }

    [Fact]
    public async Task Integers_ValidBounds_NoCon103()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Integers(0, 100); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON103");
    }

    [Fact]
    public async Task Integers_NonConstantArgs_NoCon103()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M(int a, int b) { var s = Generate.Integers(a, b); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON103");
    }

    // --- Doubles: inverted bounds ---

    [Fact]
    public async Task Doubles_InvertedConstantBounds_EmitsCon103()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Doubles(1.0, 0.5); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON103");
    }

    // --- Floats: inverted bounds ---

    [Fact]
    public async Task Floats_InvertedConstantBounds_EmitsCon103()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Floats(1f, 0f); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON103");
    }

    // --- Strings: inverted bounds (named parameters) ---

    [Fact]
    public async Task Strings_InvertedNamedBounds_EmitsCon103()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Strings(minLength: 10, maxLength: 5); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON103");
    }

    [Fact]
    public async Task Strings_ValidBounds_NoCon103()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Strings(minLength: 0, maxLength: 20); } }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON103");
    }

    // --- Code fix: swaps arguments ---

    [Fact]
    public async Task CodeFix_SwapsArguments_ForIntegers()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Integers(10, 5); } }
            """;

        string? result = await ApplyCodeFixAsync(source);

        Assert.NotNull(result);
        Assert.Contains("Generate.Integers(5, 10)", result);
    }

    [Fact]
    public async Task CodeFix_SwapsArguments_ForDoubles()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Doubles(1.0, 0.5); } }
            """;

        string? result = await ApplyCodeFixAsync(source);

        Assert.NotNull(result);
        Assert.Contains("Generate.Doubles(0.5, 1.0)", result);
    }

    [Fact]
    public async Task CodeFix_SwapsArguments_ForStringsWithNamedParams()
    {
        string source = """
            using Conjecture.Core;
            class Test { void M() { var s = Generate.Strings(minLength: 10, maxLength: 5); } }
            """;

        string? result = await ApplyCodeFixAsync(source);

        Assert.NotNull(result);
        Assert.Contains("minLength: 5", result);
        Assert.Contains("maxLength: 10", result);
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
        var analyzer = new CON103Analyzer();
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static async Task<string?> ApplyCodeFixAsync(string source)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? target = diagnostics.FirstOrDefault(d => d.Id == "CON103");
        if (target is null)
        {
            return null;
        }

        CSharpCompilation compilation = CreateCompilation(source);

        using var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId, VersionStamp.Create(), "Test", "Test", LanguageNames.CSharp,
                compilationOptions: compilation.Options,
                metadataReferences: GetReferences()));

        var documentId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(
            DocumentInfo.Create(documentId, "Test.cs",
                loader: TextLoader.From(TextAndVersion.Create(
                    SourceText.From(source), VersionStamp.Create()))));

        workspace.TryApplyChanges(solution);
        Document document = workspace.CurrentSolution.GetDocument(documentId)!;

        // Re-run the analyzer on the workspace compilation to get mapped diagnostics
        Compilation workspaceCompilation = (await document.Project.GetCompilationAsync())!;
        CompilationWithAnalyzers cwAnalyzers = workspaceCompilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new CON103Analyzer()));
        ImmutableArray<Diagnostic> mapped = await cwAnalyzers.GetAnalyzerDiagnosticsAsync();
        Diagnostic? mappedDiagnostic = mapped.FirstOrDefault(d => d.Id == "CON103");
        if (mappedDiagnostic is null)
        {
            return null;
        }

        var fix = new CON103CodeFix();
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document, mappedDiagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await fix.RegisterCodeFixesAsync(context);

        if (!actions.Any())
        {
            return null;
        }

        ImmutableArray<CodeActionOperation> operations =
            await actions[0].GetOperationsAsync(CancellationToken.None);
        foreach (CodeActionOperation op in operations)
        {
            op.Apply(workspace, CancellationToken.None);
        }

        Document updated = workspace.CurrentSolution.GetDocument(documentId)!;
        SourceText text = await updated.GetTextAsync();
        return text.ToString();
    }
}
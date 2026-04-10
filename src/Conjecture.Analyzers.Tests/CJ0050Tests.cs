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

public sealed class CJ0050Tests
{
    private const string Preamble = """
        using System;
        using System.Collections.Generic;
        namespace Conjecture.Core {
            public class Strategy<T> {
                public Strategy<T> Where(Func<T, bool> predicate) => this;
            }
            public static class Generate {
                public static Strategy<int> Integers() => new();
                public static Strategy<string> Strings() => new();
                public static Strategy<List<T>> Lists<T>() => new();
            }
        }
        using Conjecture.Core;
        """;

    // --- .Where(x => x > 0) on Strategy<int> → CJ0050 ---

    [Fact]
    public async Task WhereXGreaterThan0_OnStrategyInt_EmitsCJ0050()
    {
        string source = Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = Generate.Integers().Where(x => x > 0); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CJ0050");
    }

    // --- .Where(x => x < 0) on Strategy<int> → CJ0050 ---

    [Fact]
    public async Task WhereXLessThan0_OnStrategyInt_EmitsCJ0050()
    {
        string source = Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = Generate.Integers().Where(x => x < 0); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CJ0050");
    }

    // --- .Where(x => x != 0) on Strategy<int> → CJ0050 ---

    [Fact]
    public async Task WhereXNotEqualTo0_OnStrategyInt_EmitsCJ0050()
    {
        string source = Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = Generate.Integers().Where(x => x != 0); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CJ0050");
    }

    // --- .Where(x => x.Length > 0) on Strategy<string> → CJ0050 ---

    [Fact]
    public async Task WhereXLengthGreaterThan0_OnStrategyString_EmitsCJ0050()
    {
        string source = Preamble + """
            class Tests {
                void Foo() { Strategy<string> s = Generate.Strings().Where(x => x.Length > 0); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CJ0050");
    }

    // --- .Where(x => x.Count > 0) on Strategy<List<int>> → CJ0050 ---

    [Fact]
    public async Task WhereXCountGreaterThan0_OnStrategyList_EmitsCJ0050()
    {
        string source = Preamble + """
            class Tests {
                void Foo() { Strategy<List<int>> s = Generate.Lists<int>().Where(x => x.Count > 0); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CJ0050");
    }

    // --- .Where(x => x > 1) on Strategy<int> → no CJ0050 (custom predicate) ---

    [Fact]
    public async Task WhereCustomPredicate_OnStrategyInt_NoCJ0050()
    {
        string source = Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = Generate.Integers().Where(x => x > 1); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CJ0050");
    }

    // --- .Where(x => x > 0) on IEnumerable<int> (plain LINQ) → no CJ0050 ---

    [Fact]
    public async Task WhereXGreaterThan0_OnIEnumerableInt_NoCJ0050()
    {
        string source = Preamble + """
            using System.Collections.Generic;
            using System.Linq;
            class Tests {
                void Foo() {
                    IEnumerable<int> source = new List<int>();
                    IEnumerable<int> result = source.Where(x => x > 0);
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CJ0050");
    }

    // --- Severity is Info ---

    [Fact]
    public async Task CJ0050_IsInfoSeverity()
    {
        string source = Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = Generate.Integers().Where(x => x > 0); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? cj0050 = diagnostics.FirstOrDefault(d => d.Id == "CJ0050");

        Assert.NotNull(cj0050);
        Assert.Equal(DiagnosticSeverity.Info, cj0050.Severity);
    }

    // --- Code fix: .Where(x => x > 0) → .Positive ---

    [Fact]
    public async Task CodeFix_WherePositive_ReplacesWithPositiveProperty()
    {
        string source = Preamble + """
            class Tests {
                void Foo() { Strategy<int> s = Generate.Integers().Where(x => x > 0); }
            }
            """;

        string? result = await ApplyCodeFixAsync(source);

        Assert.NotNull(result);
        Assert.Contains(".Positive", result);
        Assert.DoesNotContain(".Where", result);
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
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Linq.dll")),
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
        CJ0050Analyzer analyzer = new();
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static async Task<string?> ApplyCodeFixAsync(string source)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? target = diagnostics.FirstOrDefault(d => d.Id == "CJ0050");
        if (target is null)
        {
            return null;
        }

        CSharpCompilation compilation = CreateCompilation(source);

        using Microsoft.CodeAnalysis.AdhocWorkspace workspace = new();
        ProjectId projectId = ProjectId.CreateNewId();
        Solution solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId, VersionStamp.Create(), "Test", "Test", LanguageNames.CSharp,
                compilationOptions: compilation.Options,
                metadataReferences: GetReferences()));

        DocumentId documentId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(
            DocumentInfo.Create(documentId, "Test.cs",
                loader: TextLoader.From(TextAndVersion.Create(
                    SourceText.From(source), VersionStamp.Create()))));

        workspace.TryApplyChanges(solution);
        Document document = workspace.CurrentSolution.GetDocument(documentId)!;

        Compilation workspaceCompilation = (await document.Project.GetCompilationAsync())!;
        CompilationWithAnalyzers cwAnalyzers = workspaceCompilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new CJ0050Analyzer()));
        ImmutableArray<Diagnostic> mapped = await cwAnalyzers.GetAnalyzerDiagnosticsAsync();
        Diagnostic? mappedDiagnostic = mapped.FirstOrDefault(d => d.Id == "CJ0050");
        if (mappedDiagnostic is null)
        {
            return null;
        }

        CJ0050CodeFix fix = new();
        List<CodeAction> actions = [];
        CodeFixContext context = new(
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
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

public sealed class CON102Tests
{
    // Stub types — no external assembly required for [Property]
    private const string Preamble = """
        using System;
        using System.Threading.Tasks;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}

        """;

    // --- .GetAwaiter().GetResult() inside [Property] → CON102 ---

    [Fact]
    public async Task GetAwaiterGetResult_InsidePropertyMethod_EmitsCon102()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { Task.Delay(0).GetAwaiter().GetResult(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON102");
    }

    [Fact]
    public async Task GetAwaiterGetResult_InsidePropertyMethod_Con102IsInfo()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { Task.Delay(0).GetAwaiter().GetResult(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? con102 = diagnostics.FirstOrDefault(d => d.Id == "CON102");

        Assert.NotNull(con102);
        Assert.Equal(DiagnosticSeverity.Info, con102.Severity);
    }

    // --- .Result on Task inside [Property] → CON102 ---

    [Fact]
    public async Task TaskResult_InsidePropertyMethod_EmitsCon102()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { int r = Task.FromResult(x).Result; }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON102");
    }

    [Fact]
    public async Task TaskResult_InsidePropertyMethod_Con102IsInfo()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { int r = Task.FromResult(x).Result; }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? con102 = diagnostics.FirstOrDefault(d => d.Id == "CON102");

        Assert.NotNull(con102);
        Assert.Equal(DiagnosticSeverity.Info, con102.Severity);
    }

    // --- .Wait() on Task inside [Property] → CON102 ---

    [Fact]
    public async Task TaskWait_InsidePropertyMethod_EmitsCon102()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { Task.Delay(0).Wait(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON102");
    }

    [Fact]
    public async Task TaskWait_InsidePropertyMethod_Con102IsInfo()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { Task.Delay(0).Wait(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? con102 = diagnostics.FirstOrDefault(d => d.Id == "CON102");

        Assert.NotNull(con102);
        Assert.Equal(DiagnosticSeverity.Info, con102.Severity);
    }

    // --- Same patterns outside [Property] → no diagnostic ---

    [Fact]
    public async Task GetAwaiterGetResult_OutsidePropertyMethod_NoCon102()
    {
        string source = Preamble + """
            class Tests {
                public void Foo(int x) { Task.Delay(0).GetAwaiter().GetResult(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON102");
    }

    [Fact]
    public async Task TaskResult_OutsidePropertyMethod_NoCon102()
    {
        string source = Preamble + """
            class Tests {
                public void Foo(int x) { int r = Task.FromResult(x).Result; }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON102");
    }

    [Fact]
    public async Task TaskWait_OutsidePropertyMethod_NoCon102()
    {
        string source = Preamble + """
            class Tests {
                public void Foo(int x) { Task.Delay(0).Wait(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON102");
    }

    // --- Code fix: GetAwaiter().GetResult() → async Task + await ---

    [Fact]
    public async Task CodeFix_GetAwaiterGetResult_ConvertsToAsyncAwait()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { Task.Delay(0).GetAwaiter().GetResult(); }
            }
            """;

        string? result = await ApplyCodeFixAsync(source);

        Assert.NotNull(result);
        Assert.Contains("async", result);
        Assert.Contains("await", result);
        Assert.DoesNotContain(".GetAwaiter()", result);
        Assert.DoesNotContain(".GetResult()", result);
    }

    [Fact]
    public async Task CodeFix_GetAwaiterGetResult_MethodReturnsTask()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { Task.Delay(0).GetAwaiter().GetResult(); }
            }
            """;

        string? result = await ApplyCodeFixAsync(source);

        Assert.NotNull(result);
        Assert.Contains("Task", result);
    }

    // --- Code fix: .Result → async Task + await ---

    [Fact]
    public async Task CodeFix_TaskResult_ConvertsToAsyncAwait()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { int r = Task.FromResult(x).Result; }
            }
            """;

        string? result = await ApplyCodeFixAsync(source);

        Assert.NotNull(result);
        Assert.Contains("async", result);
        Assert.Contains("await", result);
        Assert.DoesNotContain(".Result", result);
    }

    // --- Code fix: .Wait() → async Task + await ---

    [Fact]
    public async Task CodeFix_TaskWait_ConvertsToAsyncAwait()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { Task.Delay(0).Wait(); }
            }
            """;

        string? result = await ApplyCodeFixAsync(source);

        Assert.NotNull(result);
        Assert.Contains("async", result);
        Assert.Contains("await", result);
        Assert.DoesNotContain(".Wait()", result);
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
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Threading.Tasks.dll")),
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
        var analyzer = new CON102Analyzer();
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static async Task<string?> ApplyCodeFixAsync(string source)
    {
        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? target = diagnostics.FirstOrDefault(d => d.Id == "CON102");
        if (target is null)
        {
            return null;
        }

        CSharpCompilation compilation = CreateCompilation(source);

        using var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
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
            ImmutableArray.Create<DiagnosticAnalyzer>(new CON102Analyzer()));
        ImmutableArray<Diagnostic> mapped = await cwAnalyzers.GetAnalyzerDiagnosticsAsync();
        Diagnostic? mappedDiagnostic = mapped.FirstOrDefault(d => d.Id == "CON102");
        if (mappedDiagnostic is null)
        {
            return null;
        }

        var fix = new CON102CodeFix();
        var actions = new List<CodeAction>();
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
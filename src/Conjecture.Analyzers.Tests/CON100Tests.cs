// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using Conjecture.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers.Tests;

public sealed class CON100Tests
{
    // Stub types so tests don't need external assembly references
    private const string Preamble = """
        using System;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}
        static class Assert {
            public static void Equal<T>(T a, T b) {}
            public static void True(bool c) {}
        }
        static class FluentExtensions {
            public static FluentObj Should(this object o) => new FluentObj();
        }
        class FluentObj { public void Be(object v) {} }

        """;

    // --- Void [Property] method with Assert.Equal -> CON100 ---

    [Fact]
    public async Task VoidPropertyMethod_WithAssertEqual_EmitsCon100()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { Assert.Equal(x, x); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON100");
    }

    [Fact]
    public async Task VoidPropertyMethod_WithAssertEqual_Con100IsWarning()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { Assert.Equal(x, x); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? hyp100 = diagnostics.FirstOrDefault(d => d.Id == "CON100");

        Assert.NotNull(hyp100);
        Assert.Equal(DiagnosticSeverity.Warning, hyp100.Severity);
    }

    // --- Void [Property] method with Assert.True -> CON100 ---

    [Fact]
    public async Task VoidPropertyMethod_WithAssertTrue_EmitsCon100()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { Assert.True(x > 0); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON100");
    }

    // --- Void [Property] method with Fluent Assertions -> CON100 ---

    [Fact]
    public async Task VoidPropertyMethod_WithFluentShould_EmitsCon100()
    {
        string source = Preamble + """
            class Tests {
                [Property]
                public void Foo(int x) { x.Should().Be(x); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON100");
    }

    // --- Non-[Property] method with Assert.Equal -> no diagnostic ---

    [Fact]
    public async Task NonPropertyMethod_WithAssertEqual_NoCon100()
    {
        string source = Preamble + """
            class Tests {
                public void Foo(int x) { Assert.Equal(x, x); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON100");
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
        var analyzer = new CON100Analyzer();
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Conjecture.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Conjecture.Analyzers.Tests;

public sealed class CON105Tests
{
    private const string Preamble = """
        using System;
        using Conjecture.Core;
        [AttributeUsage(AttributeTargets.Method)] class PropertyAttribute : Attribute {}

        """;

    // --- [Property] param of type Person where Person has [Arbitrary] -> CON105 info ---

    [Fact]
    public async Task PropertyParam_TypeWithArbitrary_EmitsCon105()
    {
        string source = Preamble + """
            [Arbitrary] class Person { }
            class Tests {
                [Property]
                public bool Foo(Person p) => true;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CON105");
    }

    [Fact]
    public async Task PropertyParam_TypeWithArbitrary_Con105IsInfo()
    {
        string source = Preamble + """
            [Arbitrary] class Person { }
            class Tests {
                [Property]
                public bool Foo(Person p) => true;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);
        Diagnostic? con105 = diagnostics.FirstOrDefault(d => d.Id == "CON105");

        Assert.NotNull(con105);
        Assert.Equal(DiagnosticSeverity.Info, con105.Severity);
    }

    // --- [Property] param with [From<PersonArbitrary>] -> no diagnostic ---

    [Fact]
    public async Task PropertyParam_WithFromAttribute_NoCon105()
    {
        string source = Preamble + """
            [Arbitrary] class Person { }
            class PersonArbitrary : IStrategyProvider { }
            class Tests {
                [Property]
                public bool Foo([From<PersonArbitrary>] Person p) => true;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON105");
    }

    // --- Non-[Property] method -> no diagnostic ---

    [Fact]
    public async Task NonPropertyMethod_NoCon105()
    {
        string source = Preamble + """
            [Arbitrary] class Person { }
            class Tests {
                public bool Foo(Person p) => true;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON105");
    }

    // --- Type without [Arbitrary] -> no diagnostic ---

    [Fact]
    public async Task PropertyParam_TypeWithoutArbitrary_NoCon105()
    {
        string source = Preamble + """
            class Person { }
            class Tests {
                [Property]
                public bool Foo(Person p) => true;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CON105");
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
            MetadataReference.CreateFromFile(typeof(Conjecture.Core.ArbitraryAttribute).Assembly.Location),
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
        var analyzer = new CON105Analyzer();
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}

// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Conjecture.Generators;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class PartialConstructorExtractionTests
{
    // --- single partial constructor: happy path ---

    [Fact]
    public void Extract_SinglePartialConstructor_ReturnsPartialConstructorMode()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public partial class Widget
            {
                public string Name { get; init; } = "";
                public int Value { get; init; }
                public partial Widget(string name, int value);
            }
            """,
            "MyApp.Widget");

        (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);

        Assert.Empty(diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(model);
        Assert.Equal(ConstructionMode.PartialConstructor, model.ConstructionMode);
    }

    [Fact]
    public void Extract_SinglePartialConstructor_ExtractsMembersFromInitProperties()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public partial class Widget
            {
                public string Name { get; init; } = "";
                public int Value { get; init; }
                public partial Widget(string name, int value);
            }
            """,
            "MyApp.Widget");

        (TypeModel? model, _) = TypeModelExtractor.Extract(symbol);

        Assert.NotNull(model);
        Assert.Equal(2, model.Members.Length);
        Assert.Contains(model.Members, static m => m.Name == "Name" && m.TypeFullName == "System.String");
        Assert.Contains(model.Members, static m => m.Name == "Value" && m.TypeFullName == "System.Int32");
    }

    // --- no partial constructor: existing behaviour unaffected ---

    [Fact]
    public void Extract_NoPartialConstructor_DoesNotSetPartialConstructorMode()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            "namespace MyApp; public partial class Plain { public Plain(int x) { } }",
            "MyApp.Plain");

        (TypeModel? model, _) = TypeModelExtractor.Extract(symbol);

        Assert.NotNull(model);
        Assert.NotEqual(ConstructionMode.PartialConstructor, model.ConstructionMode);
    }

    // --- two partial constructors: CON203 ---

    [Fact]
    public void Extract_TwoPartialConstructors_ReturnsCon203Diagnostic()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public partial class Ambiguous
            {
                public int X { get; init; }
                public int Y { get; init; }
                public partial Ambiguous(int x);
                public partial Ambiguous(int x, int y);
            }
            """,
            "MyApp.Ambiguous");

        (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);

        Assert.Null(model);
        Assert.Contains(diagnostics, static d => d.Id == DiagnosticDescriptors.Con203.Id);
    }

    [Fact]
    public void Extract_TwoPartialConstructors_Con203IsError()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public partial class Ambiguous
            {
                public int X { get; init; }
                public int Y { get; init; }
                public partial Ambiguous(int x);
                public partial Ambiguous(int x, int y);
            }
            """,
            "MyApp.Ambiguous");

        (_, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);
        Diagnostic? diag = diagnostics.FirstOrDefault(static d => d.Id == DiagnosticDescriptors.Con203.Id);

        Assert.NotNull(diag);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    // --- primary constructor + partial constructor: CON204 ---

    [Fact]
    public void Extract_PrimaryAndPartialConstructor_ReturnsCon204Diagnostic()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public partial class Conflict(int x)
            {
                public int X { get; init; }
                public partial Conflict(int x, int y);
            }
            """,
            "MyApp.Conflict");

        (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);

        Assert.Null(model);
        Assert.Contains(diagnostics, static d => d.Id == DiagnosticDescriptors.Con204.Id);
    }

    [Fact]
    public void Extract_PrimaryAndPartialConstructor_Con204IsError()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public partial class Conflict(int x)
            {
                public int X { get; init; }
                public partial Conflict(int x, int y);
            }
            """,
            "MyApp.Conflict");

        (_, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);
        Diagnostic? diag = diagnostics.FirstOrDefault(static d => d.Id == DiagnosticDescriptors.Con204.Id);

        Assert.NotNull(diag);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    // --- partial constructor on non-[Arbitrary] type via generator: no Conjecture diagnostic ---

    [Fact]
    public void Generator_PartialConstructorOnNonArbitraryType_EmitsNoConjectureDiagnostic()
    {
        string source = """
            namespace MyApp;
            public partial class NotArbitrary
            {
                public int X { get; init; }
                public partial NotArbitrary(int x);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = RunGeneratorDiagnostics(source);

        Assert.DoesNotContain(diagnostics, static d =>
            d.Id.StartsWith("CON", System.StringComparison.Ordinal));
    }

    // --- full generator round-trip: CON203 and CON204 ---

    [Fact]
    public void Generator_TwoPartialConstructors_EmitsCon203()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial class Ambiguous
            {
                public int X { get; init; }
                public partial Ambiguous(int x);
                public partial Ambiguous(int x, int y);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = RunGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, static d => d.Id == DiagnosticDescriptors.Con203.Id);
    }

    [Fact]
    public void Generator_PrimaryAndPartialConstructor_EmitsCon204()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial class Conflict(int x)
            {
                public int X { get; init; }
                public partial Conflict(int x, int y);
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = RunGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, static d => d.Id == DiagnosticDescriptors.Con204.Id);
    }

    // --- helpers ---

    private static INamedTypeSymbol CompileAndGetSymbol(string source, string metadataName)
    {
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        INamedTypeSymbol? symbol = compilation.GetTypeByMetadataName(metadataName);
        Assert.NotNull(symbol);
        return symbol;
    }

    private static ImmutableArray<Diagnostic> RunGeneratorDiagnostics(string source)
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
        return result.Diagnostics;
    }
}
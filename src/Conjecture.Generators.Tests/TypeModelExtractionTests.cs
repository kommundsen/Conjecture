using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Conjecture.Generators;

namespace Conjecture.Generators.Tests;

public sealed class TypeModelExtractionTests
{
    [Fact]
    public void Extract_RecordWithPrimaryConstructor_ExtractsMemberNamesAndTypes()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            "namespace MyApp; public partial record Point(int X, int Y);",
            "MyApp.Point");

        (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(model);
        Assert.Equal(2, model.Members.Length);
        Assert.Equal("X", model.Members[0].Name);
        Assert.Equal("System.Int32", model.Members[0].TypeFullName);
        Assert.Equal("Y", model.Members[1].Name);
        Assert.Equal("System.Int32", model.Members[1].TypeFullName);
    }

    [Fact]
    public void Extract_ClassWithPublicConstructor_ExtractsMemberNamesAndTypes()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            "namespace MyApp; public partial class Person { public Person(string name, int age) { } }",
            "MyApp.Person");

        (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(model);
        Assert.Equal(2, model.Members.Length);
        Assert.Equal("name", model.Members[0].Name);
        Assert.Equal("System.String", model.Members[0].TypeFullName);
        Assert.Equal("age", model.Members[1].Name);
        Assert.Equal("System.Int32", model.Members[1].TypeFullName);
    }

    [Fact]
    public void Extract_NoAccessibleConstructor_ReturnsCon200Diagnostic()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            "namespace MyApp; public partial class Hidden { private Hidden() { } }",
            "MyApp.Hidden");

        (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);

        Assert.Null(model);
        Diagnostic? diag = diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(diag);
        Assert.Equal("CON200", diag.Id);
    }

    [Fact]
    public void Extract_NonPartialType_ReturnsCon201Diagnostic()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            "namespace MyApp; public class Solid { public Solid(int x) { } }",
            "MyApp.Solid");

        (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);

        Assert.Null(model);
        Diagnostic? diag = diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
        Assert.NotNull(diag);
        Assert.Equal("CON201", diag.Id);
    }

    [Fact]
    public void Extract_GenericType_CapturesTypeParameters()
    {
        INamedTypeSymbol symbol = CompileAndGetSymbol(
            "namespace MyApp; public partial class Box<T> { public Box(T value) { } }",
            "MyApp.Box`1");

        (TypeModel? model, ImmutableArray<Diagnostic> diagnostics) = TypeModelExtractor.Extract(symbol);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(model);
        Assert.Single(model.TypeParameters);
        Assert.Equal("T", model.TypeParameters[0]);
    }

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
}

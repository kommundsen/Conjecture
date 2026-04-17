// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Conjecture.Generators;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class HierarchyTypeModelExtractorTests
{
    [Fact]
    public void Extract_AbstractClassWithTwoArbitraryConcreteSubtypes_ReturnsModelWithBothSubtypes()
    {
        INamedTypeSymbol baseSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Shape { }
            [Arbitrary] public partial class Circle : Shape { public Circle(int radius) { } }
            [Arbitrary] public partial class Rectangle : Shape { public Rectangle(int width, int height) { } }
            """,
            "MyApp.Shape");

        INamedTypeSymbol circleSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Shape { }
            [Arbitrary] public partial class Circle : Shape { public Circle(int radius) { } }
            [Arbitrary] public partial class Rectangle : Shape { public Rectangle(int width, int height) { } }
            """,
            "MyApp.Circle");

        INamedTypeSymbol rectangleSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Shape { }
            [Arbitrary] public partial class Circle : Shape { public Circle(int radius) { } }
            [Arbitrary] public partial class Rectangle : Shape { public Rectangle(int width, int height) { } }
            """,
            "MyApp.Rectangle");

        ImmutableArray<INamedTypeSymbol> arbitrarySymbols = [circleSymbol, rectangleSymbol];
        (HierarchyTypeModel? model, ImmutableArray<Diagnostic> diagnostics) = HierarchyTypeModelExtractor.Extract(baseSymbol, arbitrarySymbols);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(model);
        Assert.Equal(2, model.Subtypes.Length);
        Assert.Equal("MyApp.Circle", model.Subtypes[0].FullyQualifiedName);
        Assert.Equal("MyApp.Rectangle", model.Subtypes[1].FullyQualifiedName);
    }

    [Fact]
    public void Extract_AbstractRecordWithArbitraryConcreteSubtypes_ReturnsModelWithSubtypes()
    {
        INamedTypeSymbol baseSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract record Figure;
            [Arbitrary] public sealed partial record Triangle(int Base, int Height) : Figure;
            [Arbitrary] public sealed partial record Diamond(int Diagonal1, int Diagonal2) : Figure;
            """,
            "MyApp.Figure");

        INamedTypeSymbol triangleSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract record Figure;
            [Arbitrary] public sealed partial record Triangle(int Base, int Height) : Figure;
            [Arbitrary] public sealed partial record Diamond(int Diagonal1, int Diagonal2) : Figure;
            """,
            "MyApp.Triangle");

        INamedTypeSymbol diamondSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract record Figure;
            [Arbitrary] public sealed partial record Triangle(int Base, int Height) : Figure;
            [Arbitrary] public sealed partial record Diamond(int Diagonal1, int Diagonal2) : Figure;
            """,
            "MyApp.Diamond");

        ImmutableArray<INamedTypeSymbol> arbitrarySymbols = [triangleSymbol, diamondSymbol];
        (HierarchyTypeModel? model, ImmutableArray<Diagnostic> diagnostics) = HierarchyTypeModelExtractor.Extract(baseSymbol, arbitrarySymbols);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(model);
        Assert.Equal(2, model.Subtypes.Length);
    }

    [Fact]
    public void Extract_AbstractBaseWithZeroQualifyingSubtypes_ReturnsNullAndDiagnostic()
    {
        INamedTypeSymbol baseSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Widget { }
            """,
            "MyApp.Widget");

        ImmutableArray<INamedTypeSymbol> arbitrarySymbols = [];
        (HierarchyTypeModel? model, ImmutableArray<Diagnostic> diagnostics) = HierarchyTypeModelExtractor.Extract(baseSymbol, arbitrarySymbols);

        Assert.Null(model);
        Assert.NotEmpty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Extract_IntermediateAbstractSubtype_ExcludesIntermediateFromModel()
    {
        INamedTypeSymbol baseSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Animal { }
            public abstract class Quadruped : Animal { }
            [Arbitrary] public partial class Dog : Quadruped { public Dog(string name) { } }
            """,
            "MyApp.Animal");

        INamedTypeSymbol dogSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Animal { }
            public abstract class Quadruped : Animal { }
            [Arbitrary] public partial class Dog : Quadruped { public Dog(string name) { } }
            """,
            "MyApp.Dog");

        ImmutableArray<INamedTypeSymbol> arbitrarySymbols = [dogSymbol];
        (HierarchyTypeModel? model, ImmutableArray<Diagnostic> diagnostics) = HierarchyTypeModelExtractor.Extract(baseSymbol, arbitrarySymbols);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(model);
        Assert.Single(model.Subtypes);
        Assert.Equal("MyApp.Dog", model.Subtypes[0].FullyQualifiedName);
    }

    [Fact]
    public void Extract_GenericAbstractBaseWithGenericConcreteSubtype_ThreadsTypeParametersThroughModel()
    {
        INamedTypeSymbol baseSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Container<T> { }
            [Arbitrary] public partial class Box<T> : Container<T> { public Box(T value) { } }
            """,
            "MyApp.Container`1");

        INamedTypeSymbol boxSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Container<T> { }
            [Arbitrary] public partial class Box<T> : Container<T> { public Box(T value) { } }
            """,
            "MyApp.Box`1");

        ImmutableArray<INamedTypeSymbol> arbitrarySymbols = [boxSymbol];
        (HierarchyTypeModel? model, ImmutableArray<Diagnostic> diagnostics) = HierarchyTypeModelExtractor.Extract(baseSymbol, arbitrarySymbols);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(model);
        Assert.Single(model.TypeParameters);
        Assert.Equal("T", model.TypeParameters[0]);
        Assert.Single(model.Subtypes);
    }

    [Fact]
    public void Extract_PopulatesHierarchyTypeModelFields()
    {
        INamedTypeSymbol baseSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Shape { }
            [Arbitrary] public partial class Circle : Shape { public Circle(int radius) { } }
            """,
            "MyApp.Shape");

        INamedTypeSymbol circleSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Shape { }
            [Arbitrary] public partial class Circle : Shape { public Circle(int radius) { } }
            """,
            "MyApp.Circle");

        ImmutableArray<INamedTypeSymbol> arbitrarySymbols = [circleSymbol];
        (HierarchyTypeModel? model, _) = HierarchyTypeModelExtractor.Extract(baseSymbol, arbitrarySymbols);

        Assert.NotNull(model);
        Assert.Equal("MyApp.Shape", model.FullyQualifiedName);
        Assert.Equal("MyApp", model.Namespace);
        Assert.Equal("Shape", model.TypeName);
    }

    [Fact]
    public void Extract_SubtypeModelPopulatesProviderTypeName()
    {
        INamedTypeSymbol baseSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Shape { }
            [Arbitrary] public partial class Circle : Shape { public Circle(int radius) { } }
            """,
            "MyApp.Shape");

        INamedTypeSymbol circleSymbol = CompileAndGetSymbol(
            """
            namespace MyApp;
            public abstract class Shape { }
            [Arbitrary] public partial class Circle : Shape { public Circle(int radius) { } }
            """,
            "MyApp.Circle");

        ImmutableArray<INamedTypeSymbol> arbitrarySymbols = [circleSymbol];
        (HierarchyTypeModel? model, _) = HierarchyTypeModelExtractor.Extract(baseSymbol, arbitrarySymbols);

        Assert.NotNull(model);
        Assert.NotEmpty(model.Subtypes);
        string providerTypeName = model.Subtypes[0].ProviderTypeName;
        Assert.NotEmpty(providerTypeName);
        Assert.EndsWith("Arbitrary", providerTypeName);
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
                MetadataReference.CreateFromFile(typeof(Conjecture.Core.ArbitraryAttribute).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        INamedTypeSymbol? symbol = compilation.GetTypeByMetadataName(metadataName);
        Assert.NotNull(symbol);
        return symbol;
    }
}
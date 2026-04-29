// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class GenerateForDiagnosticTests
{
    // --- CON310: Strategy.For<T>() where T is an interface ---

    [Fact]
    public void InterfaceTypeArgument_EmitsCon310()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public interface IShape { }
            public class Usage
            {
                public void Run() { Strategy.For<IShape>(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == DiagnosticDescriptors.Con310.Id);
    }

    [Fact]
    public void InterfaceTypeArgument_Con310IsError()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public interface IShape { }
            public class Usage
            {
                public void Run() { Strategy.For<IShape>(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? con310 = diagnostics.FirstOrDefault(d => d.Id == DiagnosticDescriptors.Con310.Id);

        Assert.NotNull(con310);
        Assert.Equal(DiagnosticSeverity.Error, con310.Severity);
    }

    [Fact]
    public void InterfaceTypeArgument_NoCodeGenerated()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public interface IShape { }
            public class Usage
            {
                public void Run() { Strategy.For<IShape>(); }
            }
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        bool hasRegistry = trees.Any(
            t => t.FilePath.EndsWith("GenerateForRegistry.g.cs", StringComparison.OrdinalIgnoreCase));
        Assert.False(hasRegistry);
    }

    [Fact]
    public void InterfaceTypeArgument_DiagnosticPointsToCallSite()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public interface IShape { }
            public class Usage
            {
                public void Run() { Strategy.For<IShape>(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? con310 = diagnostics.FirstOrDefault(d => d.Id == DiagnosticDescriptors.Con310.Id);

        Assert.NotNull(con310);
        Assert.NotEqual(Location.None, con310.Location);
    }

    // --- CON311: Strategy.For<T>() where T is abstract with no [Arbitrary] concrete subtypes ---

    [Fact]
    public void AbstractTypeWithNoArbitrarySubtypes_EmitsCon311()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public abstract class AbstractBase { }
            public class ConcreteChild : AbstractBase { }
            public class Usage
            {
                public void Run() { Strategy.For<AbstractBase>(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == DiagnosticDescriptors.Con311.Id);
    }

    [Fact]
    public void AbstractTypeWithNoArbitrarySubtypes_Con311IsError()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public abstract class AbstractBase { }
            public class ConcreteChild : AbstractBase { }
            public class Usage
            {
                public void Run() { Strategy.For<AbstractBase>(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? con311 = diagnostics.FirstOrDefault(d => d.Id == DiagnosticDescriptors.Con311.Id);

        Assert.NotNull(con311);
        Assert.Equal(DiagnosticSeverity.Error, con311.Severity);
    }

    [Fact]
    public void AbstractTypeWithNoArbitrarySubtypes_DiagnosticPointsToCallSite()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public abstract class AbstractBase { }
            public class ConcreteChild : AbstractBase { }
            public class Usage
            {
                public void Run() { Strategy.For<AbstractBase>(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? con311 = diagnostics.FirstOrDefault(d => d.Id == DiagnosticDescriptors.Con311.Id);

        Assert.NotNull(con311);
        Assert.NotEqual(Location.None, con311.Location);
    }

    // --- CON312: Strategy.For<T>() where T has no registered IStrategyProvider<T> ---

    [Fact]
    public void ConcreteTypeWithNoArbitraryAndNoProvider_EmitsCon312()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public class Order { public int Id { get; set; } }
            public class Usage
            {
                public void Run() { Strategy.For<Order>(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == DiagnosticDescriptors.Con312.Id);
    }

    [Fact]
    public void ConcreteTypeWithNoArbitraryAndNoProvider_Con312IsError()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public class Order { public int Id { get; set; } }
            public class Usage
            {
                public void Run() { Strategy.For<Order>(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? con312 = diagnostics.FirstOrDefault(d => d.Id == DiagnosticDescriptors.Con312.Id);

        Assert.NotNull(con312);
        Assert.Equal(DiagnosticSeverity.Error, con312.Severity);
    }

    [Fact]
    public void ConcreteTypeWithNoArbitraryAndNoProvider_DiagnosticPointsToCallSite()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public class Order { public int Id { get; set; } }
            public class Usage
            {
                public void Run() { Strategy.For<Order>(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? con312 = diagnostics.FirstOrDefault(d => d.Id == DiagnosticDescriptors.Con312.Id);

        Assert.NotNull(con312);
        Assert.NotEqual(Location.None, con312.Location);
    }

    // --- No diagnostic: Strategy.For<T>() where T has [Arbitrary] ---

    [Fact]
    public void ArbitraryDecoratedType_ProducesNoCon310Con311Con312()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Order(int Id, string Name);
            public class Usage
            {
                public void Run() { Strategy.For<Order>(); }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticDescriptors.Con310.Id);
        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticDescriptors.Con311.Id);
        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticDescriptors.Con312.Id);
    }

    [Fact]
    public void ArbitraryDecoratedType_RegistryEntryEmitted()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Order(int Id, string Name);
            public class Usage
            {
                public void Run() { Strategy.For<Order>(); }
            }
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        bool hasRegistry = trees.Any(
            t => t.FilePath.EndsWith("GenerateForRegistry.g.cs", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasRegistry);
    }

    // --- CON311: nested [Arbitrary] subtype should suppress CON311 ---

    [Fact]
    public void AbstractTypeWithNestedArbitrarySubtype_NoCon311()
    {
        string source = """
            using Conjecture.Core;
            using System.Threading;

            namespace Tests;

            public abstract class Shape { }

            public class ShapeContainer
            {
                [Arbitrary]
                public partial class Circle : Shape
                {
                    public double Radius { get; init; }
                }
            }

            public class Usage
            {
                public void Use() => _ = Strategy.For<Shape>();
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? con311 = diagnostics.FirstOrDefault(d => d.Id == DiagnosticDescriptors.Con311.Id);

        Assert.Null(con311);
    }

    // --- helpers ---

    private static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(string source)
    {
        (_, _, ImmutableArray<Diagnostic> diagnostics) = RunGenerator(source);
        return diagnostics;
    }

    private static (ImmutableArray<SyntaxTree> GeneratedTrees, Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics) RunGenerator(string source)
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

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new GenerateForGenerator());
        GeneratorDriverRunResult result = driver.RunGenerators(inputCompilation).GetRunResult();

        Compilation outputCompilation = inputCompilation.AddSyntaxTrees(result.GeneratedTrees);
        return (result.GeneratedTrees, outputCompilation, result.Diagnostics);
    }
}
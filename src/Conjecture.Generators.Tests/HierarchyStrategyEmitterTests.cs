// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class HierarchyStrategyEmitterTests
{
    // --- method name is Create(), matching IStrategyProvider<T>.Create() ---

    [Fact]
    public void TwoCaseHierarchy_EmittedCodeContainsCreateMethod()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("Create()", emitted);
    }

    [Fact]
    public void TwoCaseHierarchy_EmittedCodeDoesNotContainGetStrategyMethod()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.DoesNotContain("GetStrategy()", emitted);
    }

    // --- emits IStrategyProvider interface with Create() return type ---

    [Fact]
    public void TwoCaseHierarchy_ImplementsIStrategyProviderInterface()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("global::Conjecture.Core.IStrategyProvider<global::Animal>", emitted);
    }

    [Fact]
    public void TwoCaseHierarchy_CreateReturnsStrategyOfBaseType()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("global::Conjecture.Core.Strategy<global::Animal> Create()", emitted);
    }

    // --- uses Strategy.OneOf with .Select() projection, not array initialization ---

    [Fact]
    public void TwoCaseHierarchy_UsesGenerateOneOf()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("global::Conjecture.Core.Strategy.OneOf(", emitted);
    }

    [Fact]
    public void TwoCaseHierarchy_ContainsTwoSelectProjections()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        int selectCount = CountOccurrences(emitted, ".Select(");
        Assert.Equal(2, selectCount);
    }

    [Fact]
    public void TwoCaseHierarchy_EachSelectProjectionCastsToBaseType()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("(global::Animal)", emitted);
    }

    [Fact]
    public void TwoCaseHierarchy_SelectProjectionUsesStaticLambda()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("static x =>", emitted);
    }

    // --- three-case hierarchy has three arms ---

    [Fact]
    public void ThreeCaseHierarchy_ContainsThreeSelectProjections()
    {
        HierarchyTypeModel model = BuildThreeCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        int selectCount = CountOccurrences(emitted, ".Select(");
        Assert.Equal(3, selectCount);
    }

    [Fact]
    public void ThreeCaseHierarchy_ContainsThreeProviderInstantiations()
    {
        HierarchyTypeModel model = BuildThreeCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        int dogProviderCount = CountOccurrences(emitted, "new DogArbitrary()");
        int catProviderCount = CountOccurrences(emitted, "new CatArbitrary()");
        int birdProviderCount = CountOccurrences(emitted, "new BirdArbitrary()");

        Assert.Equal(1, dogProviderCount);
        Assert.Equal(1, catProviderCount);
        Assert.Equal(1, birdProviderCount);
    }

    // --- generic base types: type parameters appear in class name and interface ---

    [Fact]
    public void GenericBaseType_ClassNameIncludesTypeParameters()
    {
        HierarchyTypeModel model = BuildGenericHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("class ContainerArbitrary<T>", emitted);
    }

    [Fact]
    public void GenericBaseType_InterfaceIncludesTypeParameters()
    {
        HierarchyTypeModel model = BuildGenericHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("global::Conjecture.Core.IStrategyProvider<global::Container<T>>", emitted);
    }

    [Fact]
    public void GenericBaseType_CreateReturnTypeIncludesTypeParameters()
    {
        HierarchyTypeModel model = BuildGenericHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("global::Conjecture.Core.Strategy<global::Container<T>> Create()", emitted);
    }

    // --- file header and namespace are correct ---

    [Fact]
    public void TwoCaseHierarchy_ContainsAutoGeneratedComment()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("// <auto-generated/>", emitted);
    }

    [Fact]
    public void TwoCaseHierarchy_ContainsNullableEnable()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("#nullable enable", emitted);
    }

    [Fact]
    public void TwoCaseHierarchy_ContainsCorrectNamespace()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("namespace Zoo;", emitted);
    }

    [Fact]
    public void TwoCaseHierarchy_ClassIsInternalSealed()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);

        Assert.Contains("internal sealed class", emitted);
    }

    // --- end-to-end: emitted code compiles without errors ---

    [Fact]
    public void TwoCaseHierarchy_EmittedCodeCompiles()
    {
        HierarchyTypeModel model = BuildTwoCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);
        const string stubs = """
            using Conjecture.Core;
            public abstract class Animal { }
            public sealed class Dog : Animal { }
            public sealed class Cat : Animal { }
            public sealed class DogArbitrary : IStrategyProvider<Animal> {
                public Strategy<Animal> Create() => throw new System.NotImplementedException();
            }
            public sealed class CatArbitrary : IStrategyProvider<Animal> {
                public Strategy<Animal> Create() => throw new System.NotImplementedException();
            }
            """;

        Compilation compilation = CompileEmittedCode(emitted, stubs);
        IEnumerable<Diagnostic> errors = compilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error);

        Assert.Empty(errors);
    }

    [Fact]
    public void ThreeCaseHierarchy_EmittedCodeCompiles()
    {
        HierarchyTypeModel model = BuildThreeCaseHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);
        const string stubs = """
            using Conjecture.Core;
            public abstract class Animal { }
            public sealed class Dog : Animal { }
            public sealed class Cat : Animal { }
            public sealed class Bird : Animal { }
            public sealed class DogArbitrary : IStrategyProvider<Animal> {
                public Strategy<Animal> Create() => throw new System.NotImplementedException();
            }
            public sealed class CatArbitrary : IStrategyProvider<Animal> {
                public Strategy<Animal> Create() => throw new System.NotImplementedException();
            }
            public sealed class BirdArbitrary : IStrategyProvider<Animal> {
                public Strategy<Animal> Create() => throw new System.NotImplementedException();
            }
            """;

        Compilation compilation = CompileEmittedCode(emitted, stubs);
        IEnumerable<Diagnostic> errors = compilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error);

        Assert.Empty(errors);
    }

    [Fact]
    public void GenericBaseType_EmittedCodeCompiles()
    {
        HierarchyTypeModel model = BuildGenericHierarchyModel();
        string emitted = HierarchyStrategyEmitter.Emit(model);
        const string stubs = """
            using Conjecture.Core;
            public abstract class Container<T> { }
            public sealed class ListContainer<T> : Container<T> { }
            public sealed class ArrayContainer<T> : Container<T> { }
            public sealed class ListContainerArbitrary<T> : IStrategyProvider<Container<T>> {
                public Strategy<Container<T>> Create() => throw new System.NotImplementedException();
            }
            public sealed class ArrayContainerArbitrary<T> : IStrategyProvider<Container<T>> {
                public Strategy<Container<T>> Create() => throw new System.NotImplementedException();
            }
            """;

        Compilation compilation = CompileEmittedCode(emitted, stubs);
        IEnumerable<Diagnostic> errors = compilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error);

        Assert.Empty(errors);
    }

    // --- edge cases and guard conditions ---

    [Fact]
    public void EmptySubtypes_ThrowsArgumentException()
    {
        ImmutableArray<SubtypeModel> subtypes = ImmutableArray<SubtypeModel>.Empty;

        HierarchyTypeModel model = new(
            FullyQualifiedName: "Animal",
            Namespace: "Zoo",
            TypeName: "Animal",
            TypeParameters: ImmutableArray<string>.Empty,
            Subtypes: subtypes);

        _ = Assert.Throws<ArgumentException>(() => HierarchyStrategyEmitter.Emit(model));
    }

    [Fact]
    public void EmptyNamespace_DoesNotEmitNamespaceDeclaration()
    {
        ImmutableArray<SubtypeModel> subtypes = ImmutableArray.Create(
            new SubtypeModel(FullyQualifiedName: "DogArbitrary", ProviderTypeName: "DogArbitrary"),
            new SubtypeModel(FullyQualifiedName: "CatArbitrary", ProviderTypeName: "CatArbitrary"));

        HierarchyTypeModel model = new(
            FullyQualifiedName: "Animal",
            Namespace: "",
            TypeName: "Animal",
            TypeParameters: ImmutableArray<string>.Empty,
            Subtypes: subtypes);

        string emitted = HierarchyStrategyEmitter.Emit(model);

        // Verify no namespace declaration line exists
        int namespaceLineCount = CountOccurrences(emitted, "\nnamespace ");
        Assert.Equal(0, namespaceLineCount);
    }

    // --- helper methods ---

    private static HierarchyTypeModel BuildTwoCaseHierarchyModel()
    {
        ImmutableArray<SubtypeModel> subtypes = ImmutableArray.Create(
            new SubtypeModel(FullyQualifiedName: "DogArbitrary", ProviderTypeName: "DogArbitrary"),
            new SubtypeModel(FullyQualifiedName: "CatArbitrary", ProviderTypeName: "CatArbitrary"));

        return new HierarchyTypeModel(
            FullyQualifiedName: "Animal",
            Namespace: "Zoo",
            TypeName: "Animal",
            TypeParameters: ImmutableArray<string>.Empty,
            Subtypes: subtypes);
    }

    private static HierarchyTypeModel BuildThreeCaseHierarchyModel()
    {
        ImmutableArray<SubtypeModel> subtypes = ImmutableArray.Create(
            new SubtypeModel(FullyQualifiedName: "DogArbitrary", ProviderTypeName: "DogArbitrary"),
            new SubtypeModel(FullyQualifiedName: "CatArbitrary", ProviderTypeName: "CatArbitrary"),
            new SubtypeModel(FullyQualifiedName: "BirdArbitrary", ProviderTypeName: "BirdArbitrary"));

        return new HierarchyTypeModel(
            FullyQualifiedName: "Animal",
            Namespace: "Zoo",
            TypeName: "Animal",
            TypeParameters: ImmutableArray<string>.Empty,
            Subtypes: subtypes);
    }

    private static HierarchyTypeModel BuildGenericHierarchyModel()
    {
        ImmutableArray<SubtypeModel> subtypes = ImmutableArray.Create(
            new SubtypeModel(FullyQualifiedName: "ListContainerArbitrary", ProviderTypeName: "ListContainerArbitrary"),
            new SubtypeModel(FullyQualifiedName: "ArrayContainerArbitrary", ProviderTypeName: "ArrayContainerArbitrary"));

        return new HierarchyTypeModel(
            FullyQualifiedName: "Container<T>",
            Namespace: "Collections",
            TypeName: "Container",
            TypeParameters: ImmutableArray.Create("T"),
            Subtypes: subtypes);
    }

    private static Compilation CompileEmittedCode(string emittedCode, string? stubCode = null)
    {
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        System.Collections.Generic.List<Microsoft.CodeAnalysis.SyntaxTree> syntaxTrees =
            [CSharpSyntaxTree.ParseText(emittedCode)];

        if (stubCode is not null)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(stubCode));
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(Conjecture.Core.Strategy).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        return compilation;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
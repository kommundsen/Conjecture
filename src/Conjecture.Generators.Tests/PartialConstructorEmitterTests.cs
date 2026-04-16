// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class PartialConstructorEmitterTests
{
    // --- generated file contains both Arbitrary class and partial type block ---

    [Fact]
    public void PartialConstructorType_GeneratedFileContainsArbitraryClass()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.Contains("class WidgetArbitrary", text);
    }

    [Fact]
    public void PartialConstructorType_GeneratedFileContainsPartialTypeBlock()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.Contains("partial class Widget", text);
    }

    [Fact]
    public void PartialConstructorType_GeneratedFileContainsPartialConstructorBody()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.Contains("partial Widget()", text);
    }

    // --- Create() body uses PartialConstructorContext.Use and returns new T() ---

    [Fact]
    public void PartialConstructorType_CreateBodyUsesPartialConstructorContextUse()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.Contains("PartialConstructorContext.Use(ctx)", text);
    }

    [Fact]
    public void PartialConstructorType_CreateBodyReturnsNewT()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.Contains("return new MyApp.Widget()", text);
    }

    // --- constructor body assigns each init property from the Arbitrary's static field ---

    [Fact]
    public void PartialConstructorType_ConstructorBodyAssignsFirstProperty()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.Contains("Name =", text);
    }

    [Fact]
    public void PartialConstructorType_ConstructorBodyAssignsSecondProperty()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.Contains("Value =", text);
    }

    [Fact]
    public void PartialConstructorType_ConstructorBodyUsesPartialConstructorContextCurrentGenerate()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.Contains("PartialConstructorContext.Current.Generate(", text);
    }

    [Fact]
    public void PartialConstructorType_ConstructorBodyReferencesArbitraryStaticFields()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.Contains("WidgetArbitrary._s0", text);
        Assert.Contains("WidgetArbitrary._s1", text);
    }

    // --- strategy fields are internal static readonly ---

    [Fact]
    public void PartialConstructorType_StrategyFieldsAreInternalStaticReadonly()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.Contains("internal static readonly", text);
    }

    [Fact]
    public void PartialConstructorType_StrategyFieldsAreNotPrivate()
    {
        string text = GetGeneratedText(WidgetSource, "Widget.g.cs");

        Assert.DoesNotContain("private static readonly", text);
    }

    // --- ConstructionMode.Constructor path unchanged: no partial constructor emitted ---

    [Fact]
    public void ConstructorModeType_GeneratedFileDoesNotContainPartialConstructorBody()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial class Box { public Box(int Width, int Height) {} }",
            "Box.g.cs");

        Assert.DoesNotContain("partial Box()", text);
    }

    [Fact]
    public void ConstructorModeType_GeneratedFileDoesNotReferencePartialConstructorContext()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial class Box { public Box(int Width, int Height) {} }",
            "Box.g.cs");

        Assert.DoesNotContain("PartialConstructorContext", text);
    }

    // --- end-to-end: generated code compiles without errors ---

    [Fact]
    public void PartialConstructorType_OutputCompilationHasNoErrors()
    {
        (_, Compilation output) = RunGenerator(WidgetSource);

        IEnumerable<Diagnostic> errors = output.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    // --- zero-member PartialConstructor type ---

    [Fact]
    public void ZeroMemberPartialConstructorType_CreateBodyStillUsesPartialConstructorContextUse()
    {
        string text = GetGeneratedText(EmptyWidgetSource, "EmptyWidget.g.cs");

        Assert.Contains("PartialConstructorContext.Use(ctx)", text);
    }

    [Fact]
    public void ZeroMemberPartialConstructorType_CreateBodyStillReturnsNewT()
    {
        string text = GetGeneratedText(EmptyWidgetSource, "EmptyWidget.g.cs");

        Assert.Contains("return new MyApp.EmptyWidget()", text);
    }

    [Fact]
    public void ZeroMemberPartialConstructorType_GeneratedFileDoesNotContainPartialConstructorBody()
    {
        string text = GetGeneratedText(EmptyWidgetSource, "EmptyWidget.g.cs");

        Assert.DoesNotContain("partial EmptyWidget()", text);
    }

    // --- non-public (internal) partial type ---

    [Fact]
    public void InternalPartialConstructorType_OutputCompilationDoesNotContainCS0262()
    {
        (_, Compilation output) = RunGenerator(InternalWidgetSource);

        IEnumerable<Diagnostic> cs0262Errors = output.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error && d.Id == "CS0262");
        Assert.Empty(cs0262Errors);
    }

    [Fact]
    public void InternalPartialConstructorType_EmittedPartialBlockDoesNotStartWithPublicPartialClass()
    {
        string text = GetGeneratedText(InternalWidgetSource, "InternalWidget.g.cs");

        Assert.DoesNotContain("public partial class InternalWidget", text);
    }

    // --- source constants ---

    private const string WidgetSource = """
        using Conjecture.Core;
        namespace MyApp;
        [Arbitrary] public partial class Widget
        {
            public string Name { get; init; } = "";
            public int Value { get; init; }
            public partial Widget();
        }
        """;

    private const string EmptyWidgetSource = """
        using Conjecture.Core;
        namespace MyApp;
        [Arbitrary] public partial class EmptyWidget
        {
            public partial EmptyWidget();
        }
        """;

    private const string InternalWidgetSource = """
        using Conjecture.Core;
        namespace MyApp;
        [Arbitrary] internal partial class InternalWidget
        {
            public string Name { get; init; } = "";
            public int Value { get; init; }
            public partial InternalWidget();
        }
        """;

    // --- helpers ---

    private static string GetGeneratedText(string source, string fileName)
    {
        (ImmutableArray<SyntaxTree> trees, _) = RunGenerator(source);
        SyntaxTree? tree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tree);
        return tree.GetText().ToString();
    }

    private static (ImmutableArray<SyntaxTree> GeneratedTrees, Compilation Output) RunGenerator(string source)
    {
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        CSharpCompilation inputCompilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(Conjecture.Core.ArbitraryAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Conjecture.Core.PartialConstructorContext).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ArbitraryGenerator());
        GeneratorDriverRunResult result = driver.RunGenerators(inputCompilation).GetRunResult();

        Compilation outputCompilation = inputCompilation.AddSyntaxTrees(result.GeneratedTrees);
        return (result.GeneratedTrees, outputCompilation);
    }
}
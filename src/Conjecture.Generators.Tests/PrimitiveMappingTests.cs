// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class PrimitiveMappingTests
{
    [Fact]
    public void Record_WithGuidProperty_EmitsGuids()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Order(System.Guid Id);",
            "Order.g.cs");

        Assert.Contains("Generate.Guids()", text);
    }

    [Fact]
    public void Record_WithDecimalProperty_EmitsDecimals()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Invoice(decimal Total);",
            "Invoice.g.cs");

        Assert.Contains("Generate.Decimals()", text);
    }

    [Fact]
    public void Record_WithDateOnlyProperty_EmitsDateOnlyValues()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Event(System.DateOnly PlacedOn);",
            "Event.g.cs");

        Assert.Contains("Generate.DateOnlyValues()", text);
    }

    [Fact]
    public void Record_WithListOfStringProperty_EmitsListsOfStrings()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; using System.Collections.Generic; namespace MyApp; [Arbitrary] public partial record Tags(List<string> Items);",
            "Tags.g.cs");

        Assert.Contains("Generate.Lists(", text);
        Assert.Contains("Generate.Strings()", text);
    }

    [Fact]
    public void Record_WithDictionaryProperty_EmitsDictionaries()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; using System.Collections.Generic; namespace MyApp; [Arbitrary] public partial record Lookup(Dictionary<string, int> Map);",
            "Lookup.g.cs");

        Assert.Contains("Generate.Dictionaries(", text);
        Assert.Contains("Generate.Strings()", text);
        Assert.Contains("Generate.Integers<int>()", text);
    }

    [Fact]
    public void Record_WithImmutableArrayOfGuidProperty_EmitsImmutableArrayStrategy()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; using System.Collections.Immutable; namespace MyApp; [Arbitrary] public partial record Batch(ImmutableArray<System.Guid> Ids);",
            "Batch.g.cs");

        Assert.Contains("ImmutableArray", text);
        Assert.Contains("Generate.Guids()", text);
    }

    [Fact]
    public void Record_WithValueTupleProperty_EmitsTuples()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Pair((int, string) Value);",
            "Pair.g.cs");

        Assert.Contains("Generate.Tuples(", text);
    }

    [Fact]
    public void Record_WithUnknownCollectionType_EmitsCon202Diagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(
            "using Conjecture.Core; using System.Collections.ObjectModel; namespace MyApp; [Arbitrary] public partial record Bag(ObservableCollection<int> Items);");

        Assert.Contains(diagnostics, d => d.Id == "CON202");
    }

    private static string GetGeneratedText(string source, string fileName)
    {
        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);
        SyntaxTree? tree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tree);
        return tree.GetText().ToString();
    }

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
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Collections.Immutable.dll")),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.ObjectModel.dll")),
                MetadataReference.CreateFromFile(typeof(Conjecture.Core.ArbitraryAttribute).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ArbitraryGenerator());
        GeneratorDriverRunResult result = driver.RunGenerators(inputCompilation).GetRunResult();

        Compilation outputCompilation = inputCompilation.AddSyntaxTrees(result.GeneratedTrees);
        return (result.GeneratedTrees, outputCompilation, result.Diagnostics);
    }
}

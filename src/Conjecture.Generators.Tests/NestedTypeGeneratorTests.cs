using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class NestedTypeGeneratorTests
{
    // --- Primitive type map completeness ---

    [Fact]
    public void StringMember_EmitsGenStrings()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record W(string Name);",
            "W.g.cs");
        Assert.Contains("Gen.Strings()", text);
    }

    [Fact]
    public void BoolMember_EmitsGenBooleans()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record W(bool Flag);",
            "W.g.cs");
        Assert.Contains("Gen.Booleans()", text);
    }

    [Fact]
    public void LongMember_EmitsGenIntegersLong()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record W(long Value);",
            "W.g.cs");
        Assert.Contains("Gen.Integers<long>()", text);
    }

    [Fact]
    public void ByteMember_EmitsGenIntegersByte()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record W(byte Value);",
            "W.g.cs");
        Assert.Contains("Gen.Integers<byte>()", text);
    }

    [Fact]
    public void FloatMember_EmitsGenFloats()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record W(float Value);",
            "W.g.cs");
        Assert.Contains("Gen.Floats()", text);
    }

    [Fact]
    public void DoubleMember_EmitsGenDoubles()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record W(double Value);",
            "W.g.cs");
        Assert.Contains("Gen.Doubles()", text);
    }

    // --- List<T> ---

    [Fact]
    public void ListOfIntMember_EmitsGenListsWithInnerIntStrategy()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; using System.Collections.Generic; namespace MyApp; [Arbitrary] public partial record W(List<int> Items);",
            "W.g.cs");
        Assert.Contains("Gen.Lists<int>(", text);
        Assert.Contains("Gen.Integers<int>()", text);
    }

    // --- Enum ---

    [Fact]
    public void EnumMember_EmitsGenEnums()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; public enum Color { Red, Green, Blue } [Arbitrary] public partial record W(Color C);",
            "W.g.cs");
        Assert.Contains("Gen.Enums<", text);
        Assert.Contains("Color", text);
    }

    // --- Nullable<T> ---

    [Fact]
    public void NullableIntMember_EmitsGenNullableWithInnerIntStrategy()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record W(int? Value);",
            "W.g.cs");
        Assert.Contains("Gen.Nullable<int>(", text);
        Assert.Contains("Gen.Integers<int>()", text);
    }

    // --- Cross-type [Arbitrary] reference ---

    [Fact]
    public void MemberTypeIsArbitraryAnnotated_EmitsArbitraryProviderReference()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Address(string Street);
            [Arbitrary] public partial record Person(Address Home);
            """;

        string text = GetGeneratedText(source, "Person.g.cs");
        Assert.Contains("AddressArbitrary", text);
        Assert.Contains(".Create()", text);
    }

    [Fact]
    public void MemberTypeIsArbitraryAnnotated_OutputCompilationHasNoErrors()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Address(string Street);
            [Arbitrary] public partial record Person(Address Home);
            """;

        (_, Compilation output, _) = RunGenerator(source);
        IEnumerable<Diagnostic> errors = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    // --- Fully qualified naming ---

    [Fact]
    public void TypeInDeeplyNestedNamespace_GeneratedCodeUsesFullyQualifiedName()
    {
        string text = GetGeneratedText(
            "using Conjecture.Core; namespace MyApp.Models.Domain; [Arbitrary] public partial record Point(int X);",
            "Point.g.cs");
        Assert.Contains("MyApp.Models.Domain.Point", text);
    }

    // --- CON202 unsupported member type ---

    [Fact]
    public void UnsupportedMemberType_EmitsCon202Diagnostic()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public class Custom {}
            [Arbitrary] public partial record W(Custom Item);
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "CON202");
    }

    [Fact]
    public void UnsupportedMemberType_Con202IsWarning()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            public class Custom {}
            [Arbitrary] public partial record W(Custom Item);
            """;

        ImmutableArray<Diagnostic> diagnostics = GetGeneratorDiagnostics(source);
        Diagnostic? con202 = diagnostics.FirstOrDefault(d => d.Id == "CON202");
        Assert.NotNull(con202);
        Assert.Equal(DiagnosticSeverity.Warning, con202.Severity);
    }

    // --- helpers ---

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

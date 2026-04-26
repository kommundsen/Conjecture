// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

using Conjecture.Core;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Generators.Tests;

public sealed class ForConfigurationTests
{
    // ── ForConfiguration<T> unit tests ──────────────────────────────────────

    [Fact]
    public void Override_SingleProperty_TryGetReturnsOverriddenStrategy()
    {
        ForConfiguration<SampleRecord> cfg = new();
        Strategy<int> overrideStrategy = Generate.Just(42);

        cfg.Override(static (SampleRecord r) => r.Id, overrideStrategy);

        Strategy<int>? result = cfg.TryGet<int>("Id");
        Assert.Equal(overrideStrategy, result);
    }

    [Fact]
    public void Override_UnspecifiedProperty_TryGetReturnsNull()
    {
        ForConfiguration<SampleRecord> cfg = new();
        Strategy<int> overrideStrategy = Generate.Just(42);

        cfg.Override(static (SampleRecord r) => r.Id, overrideStrategy);

        Strategy<string>? result = cfg.TryGet<string>("Name");
        Assert.Null(result);
    }

    [Fact]
    public void Override_AllProperties_TryGetReturnsEachOverride()
    {
        ForConfiguration<SampleRecord> cfg = new();
        Strategy<int> idStrategy = Generate.Just(99);
        Strategy<string> nameStrategy = Generate.Just("test");

        cfg.Override(static (SampleRecord r) => r.Id, idStrategy)
           .Override(static (SampleRecord r) => r.Name, nameStrategy);

        Assert.Equal(idStrategy, cfg.TryGet<int>("Id"));
        Assert.Equal(nameStrategy, cfg.TryGet<string>("Name"));
    }

    [Fact]
    public void Override_ReturnsThis_ForFluentChaining()
    {
        ForConfiguration<SampleRecord> cfg = new();
        Strategy<int> s1 = Generate.Just(1);
        Strategy<string> s2 = Generate.Just("a");
        Strategy<int> s3 = Generate.Just(2);

        ForConfiguration<SampleRecord> r1 = cfg.Override(static (SampleRecord r) => r.Id, s1);
        ForConfiguration<SampleRecord> r2 = r1.Override(static (SampleRecord r) => r.Name, s2);
        ForConfiguration<SampleRecord> r3 = r2.Override(static (SampleRecord r) => r.Id, s3);

        Assert.Same(cfg, r1);
        Assert.Same(cfg, r2);
        Assert.Same(cfg, r3);
    }

    [Fact]
    public void Override_ChainThreeCalls_AllApplied()
    {
        ForConfiguration<ThreePropertyRecord> cfg = new();
        Strategy<int> s1 = Generate.Just(1);
        Strategy<string> s2 = Generate.Just("b");
        Strategy<bool> s3 = Generate.Just(true);

        cfg.Override(static (ThreePropertyRecord r) => r.Alpha, s1)
           .Override(static (ThreePropertyRecord r) => r.Beta, s2)
           .Override(static (ThreePropertyRecord r) => r.Gamma, s3);

        Assert.Equal(s1, cfg.TryGet<int>("Alpha"));
        Assert.Equal(s2, cfg.TryGet<string>("Beta"));
        Assert.Equal(s3, cfg.TryGet<bool>("Gamma"));
    }

    [Fact]
    public void TryGet_NonexistentPropertyName_ReturnsNull()
    {
        ForConfiguration<SampleRecord> cfg = new();

        Strategy<string>? result = cfg.TryGet<string>("DoesNotExist");

        Assert.Null(result);
    }

    [Fact]
    public void Override_InvalidMemberExpression_ThrowsInvalidOperationException()
    {
        ForConfiguration<SampleRecord> cfg = new();
        Strategy<int> strategy = Generate.Just(0);

        // Expression that doesn't refer to a direct member — e.g. a constant expression
        Expression<Func<SampleRecord, int>> badExpr = static _ => 42;

        bool threw = false;
        try
        {
            cfg.Override(badExpr, strategy);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw, "Expected InvalidOperationException for a non-member expression.");
    }

    // ── Gen.For<T>(Action<ForConfiguration<T>>) overload ────────────────────

    [Fact]
    public void GenFor_WithConfigureOverload_NullDelegate_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => Generate.For<SampleRecord>(null!));
    }

    [Fact]
    public void GenFor_WithConfigureOverload_ReturnsNonNullStrategy()
    {
        Strategy<SampleRecord> strategy = Generate.For<SampleRecord>(
            static cfg => cfg.Override(static (SampleRecord r) => r.Id, Generate.Just(7)));

        Assert.NotNull(strategy);
    }

    // ── GenerateForRegistry.ResolveWithOverrides ─────────────────────────────────

    [Fact]
    public void ResolveWithOverrides_UnregisteredType_ThrowsInvalidOperationException()
    {
        ForConfiguration<UnregisteredType> cfg = new();

        Assert.Throws<InvalidOperationException>(
            () => GenerateForRegistry.ResolveWithOverrides(cfg));
    }

    // ── Generator emission: CreateWithOverrides ──────────────────────────────

    [Fact]
    public void Generator_Record_EmitsCreateWithOverridesMethod()
    {
        string text = GetGeneratedArbitraryText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Order(int Id, string Customer, decimal Total);",
            "OrderArbitrary.g.cs");

        Assert.Contains("CreateWithOverrides", text);
    }

    [Fact]
    public void Generator_Record_CreateWithOverridesAcceptsForConfigurationParameter()
    {
        string text = GetGeneratedArbitraryText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Order(int Id, string Customer);",
            "OrderArbitrary.g.cs");

        Assert.Contains("ForConfiguration<", text);
        Assert.Contains("Order>", text);
    }

    [Fact]
    public void Generator_Record_CreateWithOverridesCallsTryGetForEachProperty()
    {
        string text = GetGeneratedArbitraryText(
            "using Conjecture.Core; namespace MyApp; [Arbitrary] public partial record Order(int Id, string Customer);",
            "OrderArbitrary.g.cs");

        Assert.Contains("TryGet", text);
        Assert.Contains("\"Id\"", text);
        Assert.Contains("\"Customer\"", text);
    }

    [Fact]
    public void Generator_Record_CreateWithOverridesCompilesWithoutErrors()
    {
        string source = """
            using Conjecture.Core;
            namespace MyApp;
            [Arbitrary] public partial record Order(int Id, string Customer, decimal Total);
            """;
        (_, Compilation output, _) = RunGenerator(source);

        ImmutableArray<Diagnostic> errors = output.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        Assert.Empty(errors);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GetGeneratedArbitraryText(string source, string fileName)
    {
        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);
        SyntaxTree? tree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tree);
        return tree.GetText().ToString();
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

    // ── Supporting types for unit tests (local to test assembly) ─────────────

    private sealed record SampleRecord(int Id, string Name);
    private sealed record ThreePropertyRecord(int Alpha, string Beta, bool Gamma);
    private sealed class UnregisteredType;
}
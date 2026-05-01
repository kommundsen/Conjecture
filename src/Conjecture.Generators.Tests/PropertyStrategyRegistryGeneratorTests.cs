// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Conjecture.Abstractions.Testing;

namespace Conjecture.Generators.Tests;

/// <summary>
/// Tests for <see cref="PropertyStrategyRegistryGenerator"/>, which emits an AOT-safe
/// <c>ConjectureStrategyRegistry.g.cs</c> for any <c>[Property]</c> method whose parameter
/// types have a known <c>IStrategyProvider&lt;T&gt;</c> in the referenced assemblies.
/// </summary>
public sealed class PropertyStrategyRegistryGeneratorTests
{
    // Stubs Conjecture.Xunit.PropertyAttribute in source — the real assembly is not
    // referenced by the generator test project, so there is no duplicate-type conflict.
    // Implements IPropertyTest so the generator's interface-based detection fires.
    private const string PropertyAttributeStub = """
        namespace Conjecture.Xunit
        {
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class PropertyAttribute : System.Attribute, global::Conjecture.Core.IPropertyTest
            {
                public int MaxExamples { get; set; }
                public ulong Seed { get; set; }
                public bool Database { get; set; }
                public int MaxStrategyRejections { get; set; }
                public int DeadlineMs { get; set; }
                public bool Targeting { get; set; }
                public double TargetingProportion { get; set; }
            }
        }
        """;

    [Fact]
    public void Generator_WithPropertyMethodHavingTimeProviderParam_EmitsRegistryFile()
    {
        string source = """
            using Conjecture.Xunit;
            namespace MyTests
            {
                public class PropertyTests
                {
                    [Property]
                    public void ClockTest(System.TimeProvider clock) { }
                }
            }
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        Assert.Contains(trees, t => t.FilePath.EndsWith("ConjectureStrategyRegistry.g.cs", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generator_WithPropertyMethodHavingTimeProviderParam_EmittedCodeContainsTimeProviderResolveArm()
    {
        string source = """
            using Conjecture.Xunit;
            namespace MyTests
            {
                public class PropertyTests
                {
                    [Property]
                    public void ClockTest(System.TimeProvider clock) { }
                }
            }
            """;

        string text = GetGeneratedText(source, "ConjectureStrategyRegistry.g.cs");

        Assert.Contains("typeof(global::System.TimeProvider)", text);
    }

    [Fact]
    public void Generator_WithPropertyMethodHavingTimeProviderParam_EmittedCodeContainsModuleInitializer()
    {
        string source = """
            using Conjecture.Xunit;
            namespace MyTests
            {
                public class PropertyTests
                {
                    [Property]
                    public void ClockTest(System.TimeProvider clock) { }
                }
            }
            """;

        string text = GetGeneratedText(source, "ConjectureStrategyRegistry.g.cs");

        Assert.Contains("ModuleInitializer", text);
    }

    [Fact]
    public void Generator_WithPropertyMethodHavingTimeProviderParam_EmittedCodeRegistersWithConjectureStrategyRegistrar()
    {
        string source = """
            using Conjecture.Xunit;
            namespace MyTests
            {
                public class PropertyTests
                {
                    [Property]
                    public void ClockTest(System.TimeProvider clock) { }
                }
            }
            """;

        string text = GetGeneratedText(source, "ConjectureStrategyRegistry.g.cs");

        Assert.Contains("global::Conjecture.Core.ConjectureStrategyRegistrar.Register", text);
    }

    [Fact]
    public void Generator_WithNoPropertyMethods_EmitsNoRegistryFile()
    {
        string source = """
            namespace MyTests
            {
                public class PlainTests
                {
                    public void PlainMethod(System.TimeProvider clock) { }
                }
            }
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        Assert.DoesNotContain(trees, t => t.FilePath.EndsWith("ConjectureStrategyRegistry.g.cs", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generator_WithPropertyMethodHavingOnlyBuiltInParams_EmitsNoRegistryFile()
    {
        string source = """
            using Conjecture.Xunit;
            namespace MyTests
            {
                public class PropertyTests
                {
                    [Property]
                    public void SimpleTest(int x, string y) { }
                }
            }
            """;

        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);

        Assert.DoesNotContain(trees, t => t.FilePath.EndsWith("ConjectureStrategyRegistry.g.cs", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generator_WithPropertyMethodHavingTimeProviderParam_OutputCompilationHasNoErrors()
    {
        string source = """
            using Conjecture.Xunit;
            namespace MyTests
            {
                public class PropertyTests
                {
                    [Property]
                    public void ClockTest(System.TimeProvider clock) { }
                }
            }
            """;

        (_, Compilation output, _) = RunGenerator(source);

        IEnumerable<Diagnostic> errors = output.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    private static string GetGeneratedText(string source, string fileName)
    {
        (ImmutableArray<SyntaxTree> trees, _, _) = RunGenerator(source);
        SyntaxTree? tree = trees.FirstOrDefault(
            t => t.FilePath.EndsWith(fileName, System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tree);
        return tree.GetText().ToString();
    }

    private static (ImmutableArray<SyntaxTree> GeneratedTrees, Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics) RunGenerator(string source)
    {
        CSharpCompilation inputCompilation = CreateCompilation(source, includeTime: true);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new PropertyStrategyRegistryGenerator());
        GeneratorDriverRunResult result = driver.RunGenerators(inputCompilation).GetRunResult();
        Compilation outputCompilation = inputCompilation.AddSyntaxTrees(result.GeneratedTrees);
        return (result.GeneratedTrees, outputCompilation, result.Diagnostics);
    }

    private static CSharpCompilation CreateCompilation(string source, bool includeTime = false)
    {
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(typeof(Conjecture.Core.ArbitraryAttribute).Assembly.Location),
        ];

        if (includeTime)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(Conjecture.Time.TimeProviderArbitrary).Assembly.Location));
        }

        List<SyntaxTree> trees = [CSharpSyntaxTree.ParseText(PropertyAttributeStub)];
        if (source.Length > 0)
        {
            trees.Add(CSharpSyntaxTree.ParseText(source));
        }

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: trees,
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }
}
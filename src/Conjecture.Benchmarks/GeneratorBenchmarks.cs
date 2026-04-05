// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;
using Conjecture.Core;
using Conjecture.Core.Internal;
using Conjecture.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Conjecture.Benchmarks;

/// <summary>
/// Measures ArbitraryGenerator compilation overhead: cold driver run vs incremental
/// (cached) driver run with 0, 10, or 50 [Arbitrary] types in the compilation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class GeneratorCompilationBenchmarks
{
    [Params(0, 10, 50)]
    public int TypeCount { get; set; }

    private CSharpCompilation compilation = null!;
    private GeneratorDriver warmDriver = null!;

    [GlobalSetup]
    public void Setup()
    {
        compilation = CreateCompilation(BuildSource(TypeCount));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ArbitraryGenerator());
        warmDriver = driver.RunGenerators(compilation);
    }

    /// <summary>New driver instance each call — no incremental cache.</summary>
    [Benchmark(Baseline = true)]
    public GeneratorDriverRunResult ColdRun()
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ArbitraryGenerator());
        return driver.RunGenerators(compilation).GetRunResult();
    }

    /// <summary>Reused driver — exercises the incremental/cached generator path.</summary>
    [Benchmark]
    public GeneratorDriverRunResult IncrementalRun()
    {
        return warmDriver.RunGenerators(compilation).GetRunResult();
    }

    private static string BuildSource(int typeCount)
    {
        StringBuilder sb = new();
        sb.AppendLine("using Conjecture.Core; namespace Bench;");
        for (int i = 0; i < typeCount; i++)
        {
            sb.AppendLine($"[Arbitrary] public partial record Type{i}(int X, int Y);");
        }
        return sb.ToString();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return CSharpCompilation.Create(
            assemblyName: "BenchAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(ArbitraryAttribute).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }
}

/// <summary>
/// Throughput: Generate.Tuples (hand-written) vs Generate.Compose (the pattern the source
/// generator emits for every [Arbitrary] record). Each iteration draws 100 values so
/// per-draw overhead dominates over ConjectureData setup cost.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class GeneratorThroughputBenchmarks
{
    private const int Draws = 100;

    private SplittableRandom rng = null!;
    private Strategy<(int, int)> handWritten = null!;
    private Strategy<(int, int)> generated = null!;

    [GlobalSetup]
    public void Setup()
    {
        rng = new SplittableRandom(42UL);
        handWritten = Generate.Tuples(Generate.Integers<int>(), Generate.Integers<int>());
        generated = new PairArbitrary().Create();
    }

    /// <summary>Direct Generate.Tuples — hand-written strategy baseline.</summary>
    [Benchmark(Baseline = true)]
    public (int, int) HandWritten()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        (int, int) last = default;
        for (int i = 0; i < Draws; i++)
        {
            last = handWritten.Generate(data);
        }
        return last;
    }

    /// <summary>Generate.Compose — mirrors the pattern every source-generated provider uses.</summary>
    [Benchmark]
    public (int, int) Generated()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        (int, int) last = default;
        for (int i = 0; i < Draws; i++)
        {
            last = generated.Generate(data);
        }
        return last;
    }
}

/// <summary>
/// Hand-written equivalent of what ArbitraryGenerator now emits for a two-int record:
/// static readonly strategy fields captured outside the lambda.
/// </summary>
internal sealed class PairArbitrary : IStrategyProvider<(int, int)>
{
    private static readonly Strategy<int> S0 = Generate.Integers<int>();
    private static readonly Strategy<int> S1 = Generate.Integers<int>();

    public Strategy<(int, int)> Create() =>
        Generate.Compose<(int, int)>(ctx => (ctx.Generate(S0), ctx.Generate(S1)));
}
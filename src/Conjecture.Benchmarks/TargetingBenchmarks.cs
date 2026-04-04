// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using BenchmarkDotNet.Attributes;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Benchmarks;

/// <summary>
/// End-to-end throughput benchmarks for the targeting pipeline (generation + hill climbing).
/// Each benchmark runs a full <see cref="TestRunner.Run"/> with a fixed seed and 100 examples,
/// split evenly between the generation and targeting phases.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class TargetingBenchmarks
{
    private ConjectureSettings settings = null!;
    private Strategy<int> intStrategy = null!;

    [GlobalSetup]
    public void Setup()
    {
        settings = new ConjectureSettings
        {
            Seed = 42UL,
            MaxExamples = 100,
            Targeting = true,
            TargetingProportion = 0.5,
            UseDatabase = false,
        };
        intStrategy = Generate.Integers<int>(0, 1000);
    }

    /// <summary>100-example run with one <c>Target.Maximize</c> call per example (single label).</summary>
    [Benchmark]
    public async Task TargetedGeneration_SingleLabel()
    {
        await TestRunner.Run(settings, data =>
        {
            int n = intStrategy.Generate(data);
            Target.Maximize(n);
        });
    }

    /// <summary>100-example run with three <c>Target.Maximize</c> calls per example (three labels, round-robin).</summary>
    [Benchmark]
    public async Task TargetedGeneration_MultiLabel()
    {
        await TestRunner.Run(settings, data =>
        {
            int a = intStrategy.Generate(data);
            int b = intStrategy.Generate(data);
            int c = intStrategy.Generate(data);
            Target.Maximize(a, "a");
            Target.Maximize(b, "b");
            Target.Maximize(c, "c");
        });
    }
}

/// <summary>
/// Benchmarks for <see cref="HillClimber.Climb"/> in isolation.
/// The evaluate function is a pure arithmetic score so only climber overhead is measured.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class HillClimberBenchmarks
{
    private IReadOnlyList<IRNode> singleNode = null!;
    private IReadOnlyList<IRNode> manyNodes = null!;

    [GlobalSetup]
    public void Setup()
    {
        singleNode = [IRNode.ForInteger(50, 0, 100)];
        manyNodes = Enumerable.Range(0, 50)
            .Select(i => IRNode.ForInteger((ulong)i, 0, 100))
            .ToArray();
    }

    /// <summary>100 rounds of hill climbing on a single Integer node in [0, 100].</summary>
    [Benchmark]
    public async Task HillClimber_SingleNode()
    {
        await HillClimber.Climb(
            singleNode, 50.0, "val",
            static nodes =>
            {
                double score = (double)nodes[0].Value;
                return Task.FromResult<(Status, IReadOnlyDictionary<string, double>)>(
                    (Status.Valid, new Dictionary<string, double> { ["val"] = score }));
            },
            budget: 100);
    }

    /// <summary>100 rounds of hill climbing on 50 Integer nodes, each in [0, 100], scored by sum.</summary>
    [Benchmark]
    public async Task HillClimber_ManyNodes()
    {
        double initialScore = 0;
        foreach (IRNode node in manyNodes)
        {
            initialScore += (double)node.Value;
        }

        await HillClimber.Climb(
            manyNodes, initialScore, "sum",
            static nodes =>
            {
                double score = 0;
                foreach (IRNode node in nodes)
                {
                    score += (double)node.Value;
                }
                return Task.FromResult<(Status, IReadOnlyDictionary<string, double>)>(
                    (Status.Valid, new Dictionary<string, double> { ["sum"] = score }));
            },
            budget: 100);
    }
}

/// <summary>
/// Throughput benchmarks for <see cref="Generate.Recursive{T}"/> at varying depths.
/// Uses an expression tree ADT (Literal, Add, Mul). One tree is generated per iteration;
/// BenchmarkDotNet reports operations per second.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class RecursiveGenerationBenchmarks
{
    private abstract class Expr { }
    private sealed class Literal(int value) : Expr { internal int Value { get; } = value; }
    private sealed class Add(Expr left, Expr right) : Expr { internal Expr Left { get; } = left; internal Expr Right { get; } = right; }
    private sealed class Mul(Expr left, Expr right) : Expr { internal Expr Left { get; } = left; internal Expr Right { get; } = right; }

    private SplittableRandom rng = null!;
    private Strategy<Expr> exprDepth5 = null!;
    private Strategy<Expr> exprDepth10 = null!;

    [GlobalSetup]
    public void Setup()
    {
        rng = new SplittableRandom(42UL);
        Strategy<Expr> baseCase = Generate.Integers<int>(0, 100).Select(n => (Expr)new Literal(n));
        exprDepth5 = Generate.Recursive<Expr>(
            baseCase,
            self => Generate.OneOf(
                baseCase,
                Generate.Tuples(self, self).Select(t => (Expr)new Add(t.Item1, t.Item2)),
                Generate.Tuples(self, self).Select(t => (Expr)new Mul(t.Item1, t.Item2))),
            maxDepth: 5);
        exprDepth10 = Generate.Recursive<Expr>(
            baseCase,
            self => Generate.OneOf(
                baseCase,
                Generate.Tuples(self, self).Select(t => (Expr)new Add(t.Item1, t.Item2)),
                Generate.Tuples(self, self).Select(t => (Expr)new Mul(t.Item1, t.Item2))),
            maxDepth: 10);
    }

    /// <summary>Single expression tree generated at maxDepth=5.</summary>
    [Benchmark(Baseline = true)]
    public object RecursiveGeneration_Depth5()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        return exprDepth5.Generate(data);
    }

    /// <summary>Single expression tree generated at maxDepth=10.</summary>
    [Benchmark]
    public object RecursiveGeneration_Depth10()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        return exprDepth10.Generate(data);
    }
}

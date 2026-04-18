// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using BenchmarkDotNet.Attributes;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Benchmarks;

/// <summary>
/// Allocation micro-benchmarks for strategy composition wrappers
/// (<see cref="StrategyExtensions.Select"/>, <see cref="StrategyExtensions.Where"/>,
/// <see cref="StrategyExtensions.SelectMany"/>, <see cref="Generate.Recursive"/>).
/// Establishes the per-wrapper allocation floor so regressions are visible.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class StrategyCompositionBenchmarks
{
    private SplittableRandom rng = null!;
    private Strategy<int> integersBaseline = null!;
    private Strategy<int> selectSingle = null!;
    private Strategy<int> whereSingle = null!;
    private Strategy<int> selectManySingle = null!;
    private Strategy<string> chainThreeOps = null!;
    private Strategy<int> recursiveDepth5 = null!;

    [GlobalSetup]
    public void Setup()
    {
        rng = new SplittableRandom(42UL);
        integersBaseline = Generate.Integers<int>();
        selectSingle = Generate.Integers<int>().Select(x => x + 1);
        whereSingle = Generate.Integers<int>(0, 100).Where(x => x > 50);
        selectManySingle = Generate.Integers<int>().SelectMany(x => Generate.Integers<int>(0, x));
        chainThreeOps = Generate.Integers<int>().Select(x => x * 2).Where(x => x > 10).Select(x => x.ToString());
        recursiveDepth5 = Generate.Recursive(Generate.Integers<int>(), s => s.Select(x => x + 1), maxDepth: 5);
    }

    [Benchmark(Baseline = true)]
    public int Integers_Baseline()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        return integersBaseline.Generate(data);
    }

    [Benchmark]
    public int Select_Single()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        return selectSingle.Generate(data);
    }

    [Benchmark]
    public int Where_Single()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        return whereSingle.Generate(data);
    }

    [Benchmark]
    public int SelectMany_Single()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        return selectManySingle.Generate(data);
    }

    [Benchmark]
    public string Chain_ThreeOps()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        return chainThreeOps.Generate(data);
    }

    [Benchmark]
    public int Recursive_Depth5()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        return recursiveDepth5.Generate(data);
    }
}

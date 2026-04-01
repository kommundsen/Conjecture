// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using BenchmarkDotNet.Attributes;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Benchmarks;

/// <summary>
/// Baseline throughput for ConjectureData primitive generation and strategy generation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class CoreGenerationBenchmarks
{
    private SplittableRandom rng = null!;
    private Strategy<int> integers = null!;
    private Strategy<bool> booleans = null!;

    [GlobalSetup]
    public void Setup()
    {
        rng = new SplittableRandom(42UL);
        integers = Generate.Integers<int>();
        booleans = Generate.Booleans();
    }

    // --- ConjectureData raw generation ---

    [Benchmark]
    public ulong NextInteger()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return data.NextInteger(0, ulong.MaxValue);
    }

    [Benchmark]
    public bool NextBoolean()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return data.NextBoolean();
    }

    [Params(8, 64)]
    public int ByteLength { get; set; }

    [Benchmark]
    public byte[] NextBytes()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return data.NextBytes(ByteLength);
    }

    // --- Strategy<T> generation ---

    [Benchmark]
    public int IntegerStrategyGenerate()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return integers.Generate(data);
    }

    [Benchmark]
    public bool BooleanStrategyGenerate()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return booleans.Generate(data);
    }
}
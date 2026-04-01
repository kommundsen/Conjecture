// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Benchmarks;

/// <summary>
/// Phase 2 shrinker baselines: total shrink time for standard failing properties.
/// Each benchmark runs the full TestRunner loop (generation + shrinking) with a
/// fixed seed so results are deterministic and comparable across runs.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class ShrinkingBenchmarks
{
    private ConjectureSettings settings = null!;

    [GlobalSetup]
    public void Setup()
    {
        settings = new ConjectureSettings
        {
            MaxExamples = 200,
            Seed = 1UL,
            UseDatabase = false,
        };
    }

    /// <summary>Int > 42 — exercises LexMinimize + IntegerReduction passes.</summary>
    [Benchmark(Baseline = true)]
    public async Task ShrinkInt_GreaterThanThreshold()
    {
        await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 10_000);
            if (v > 42) { throw new Exception("fail"); }
        });
    }

    /// <summary>Float > 100.0 — exercises FloatSimplificationPass.</summary>
    [Benchmark]
    public async Task ShrinkFloat_GreaterThanThreshold()
    {
        await TestRunner.Run(settings, data =>
        {
            ulong raw = data.NextFloat64(0UL, ulong.MaxValue);
            double x = Unsafe.BitCast<ulong, double>(raw);
            if (x > 100.0) { throw new Exception("fail"); }
        });
    }

    /// <summary>String containing "err" — exercises StringAwarePass.</summary>
    [Benchmark]
    public async Task ShrinkString_ContainsSubstring()
    {
        Strategy<string> strategy = Generate.Strings(alphabet: "er");
        await TestRunner.Run(settings, data =>
        {
            string s = strategy.Generate(data);
            if (s.Contains("err", StringComparison.Ordinal)) { throw new Exception("fail"); }
        });
    }

    /// <summary>List sum > 100 — exercises IntervalDeletion + BlockSwapping + Redistribution.</summary>
    [Benchmark]
    public async Task ShrinkList_SumGreaterThanThreshold()
    {
        Strategy<List<int>> strategy = Generate.Lists(Generate.Integers<int>(0, 200));
        await TestRunner.Run(settings, data =>
        {
            List<int> xs = strategy.Generate(data);
            if (xs.Sum() > 100) { throw new Exception("fail"); }
        });
    }

    /// <summary>Two-int pair sum — exercises RedistributionPass across adjacent nodes.</summary>
    [Benchmark]
    public async Task ShrinkTwoInts_SumGreaterThanThreshold()
    {
        await TestRunner.Run(settings, data =>
        {
            ulong a = data.NextInteger(0, 500);
            ulong b = data.NextInteger(0, 500);
            if (a + b > 100) { throw new Exception("fail"); }
        });
    }
}

/// <summary>
/// Overhead of async [Property] vs sync: compares TestRunner.Run (sync delegate)
/// against TestRunner.RunAsync (async delegate) on the same failing property.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class AsyncPropertyOverheadBenchmarks
{
    private ConjectureSettings settings = null!;

    [GlobalSetup]
    public void Setup()
    {
        settings = new ConjectureSettings
        {
            MaxExamples = 100,
            Seed = 2UL,
            UseDatabase = false,
        };
    }

    [Benchmark(Baseline = true)]
    public async Task SyncProperty_Passing()
    {
        await TestRunner.Run(settings, data =>
        {
            int v = Generate.Integers<int>(0, 100).Generate(data);
            _ = v;
        });
    }

    [Benchmark]
    public async Task AsyncProperty_Passing()
    {
        await TestRunner.RunAsync(settings, async data =>
        {
            int v = Generate.Integers<int>(0, 100).Generate(data);
            await Task.Yield();
            _ = v;
        });
    }

    [Benchmark]
    public async Task SyncProperty_Failing()
    {
        await TestRunner.Run(settings, data =>
        {
            ulong v = data.NextInteger(0, 10_000);
            if (v > 5) { throw new Exception("fail"); }
        });
    }

    [Benchmark]
    public async Task AsyncProperty_Failing()
    {
        await TestRunner.RunAsync(settings, async data =>
        {
            ulong v = data.NextInteger(0, 10_000);
            await Task.Yield();
            if (v > 5) { throw new Exception("fail"); }
        });
    }
}
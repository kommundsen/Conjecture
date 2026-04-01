// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using BenchmarkDotNet.Attributes;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Benchmarks;

/// <summary>
/// Phase 1 strategy generation and formatter lookup baselines.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class Phase1GenerationBenchmarks
{
    private SplittableRandom rng = null!;
    private Strategy<double> doubles = null!;
    private Strategy<float> floats = null!;
    private Strategy<string> strings = null!;
    private Strategy<List<int>> lists = null!;

    [GlobalSetup]
    public void Setup()
    {
        rng = new SplittableRandom(42UL);
        doubles = Generate.Doubles();
        floats = Generate.Floats();
        strings = Generate.Strings();
        lists = Generate.Lists(Generate.Integers<int>());
    }

    [Benchmark]
    public double FloatingPointDoubleNext()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return doubles.Generate(data);
    }

    [Benchmark]
    public float FloatingPointFloatNext()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return floats.Generate(data);
    }

    [Benchmark]
    public string StringNext()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return strings.Generate(data);
    }

    [Params(5, 20)]
    public int ListSize { get; set; }

    [Benchmark]
    public List<int> ListNext()
    {
        var bounded = Generate.Lists(Generate.Integers<int>(), minSize: ListSize, maxSize: ListSize);
        var data = ConjectureData.ForGeneration(rng.Split());
        return bounded.Generate(data);
    }
}

/// <summary>
/// FormatterRegistry lookup baselines (hot path: already-registered type).
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class FormatterRegistryBenchmarks
{
    [Benchmark]
    public string? GetInt() => FormatterRegistry.Get<int>()?.Format(42);

    [Benchmark]
    public string? GetDouble() => FormatterRegistry.Get<double>()?.Format(3.14);

    [Benchmark]
    public string? GetString() => FormatterRegistry.Get<string>()?.Format("hello");

    [Benchmark]
    public IStrategyFormatter<long>? GetUnregistered() => FormatterRegistry.Get<long>();
}

/// <summary>
/// ExampleDatabase Save/Load round-trip baselines.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class ExampleDatabaseBenchmarks : IDisposable
{
    private string dbPath = null!;
    private ExampleDatabase db = null!;
    private static readonly byte[] SampleBuffer = new byte[64];

    [GlobalSetup]
    public void Setup()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"conjecture_bench_{Guid.NewGuid():N}.db");
        db = new ExampleDatabase(dbPath);
        // Pre-populate one entry for Load benchmark
        db.Save("bench_test_id", SampleBuffer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        db.Dispose();
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        string? walPath = dbPath + "-wal";
        string? shmPath = dbPath + "-shm";
        if (File.Exists(walPath))
        {
            File.Delete(walPath);
        }

        if (File.Exists(shmPath))
        {
            File.Delete(shmPath);
        }
    }

    [Benchmark]
    public void Save()
    {
        db.Save("bench_test_id", SampleBuffer);
    }

    [Benchmark]
    public IReadOnlyList<byte[]> Load()
    {
        return db.Load("bench_test_id");
    }

    public void Dispose() => Cleanup();
}
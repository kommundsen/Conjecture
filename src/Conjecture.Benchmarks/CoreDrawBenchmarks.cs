using BenchmarkDotNet.Attributes;
using Conjecture.Core;
using Conjecture.Core.Generation;
using Conjecture.Core.Internal;

namespace Conjecture.Benchmarks;

/// <summary>
/// Baseline throughput for ConjectureData draws and strategy generation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class CoreDrawBenchmarks
{
    private SplittableRandom rng = null!;
    private Strategy<int> integers = null!;
    private Strategy<bool> booleans = null!;

    [GlobalSetup]
    public void Setup()
    {
        rng = new SplittableRandom(42UL);
        integers = Gen.Integers<int>();
        booleans = Gen.Booleans();
    }

    // --- ConjectureData raw draws ---

    [Benchmark]
    public ulong DrawInteger()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return data.DrawInteger(0, ulong.MaxValue);
    }

    [Benchmark]
    public bool DrawBoolean()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return data.DrawBoolean();
    }

    [Params(8, 64)]
    public int ByteLength { get; set; }

    [Benchmark]
    public byte[] DrawBytes()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return data.DrawBytes(ByteLength);
    }

    // --- Strategy<T> generation ---

    [Benchmark]
    public int IntegerStrategyNext()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return integers.Next(data);
    }

    [Benchmark]
    public bool BooleanStrategyNext()
    {
        var data = ConjectureData.ForGeneration(rng.Split());
        return booleans.Next(data);
    }
}

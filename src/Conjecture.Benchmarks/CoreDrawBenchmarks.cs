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
    private SplittableRandom _rng = null!;
    private Strategy<int> _integers = null!;
    private Strategy<bool> _booleans = null!;

    [GlobalSetup]
    public void Setup()
    {
        _rng = new SplittableRandom(42UL);
        _integers = Gen.Integers<int>();
        _booleans = Gen.Booleans();
    }

    // --- ConjectureData raw draws ---

    [Benchmark]
    public ulong DrawInteger()
    {
        var data = ConjectureData.ForGeneration(_rng.Split());
        return data.DrawInteger(0, ulong.MaxValue);
    }

    [Benchmark]
    public bool DrawBoolean()
    {
        var data = ConjectureData.ForGeneration(_rng.Split());
        return data.DrawBoolean();
    }

    [Params(8, 64)]
    public int ByteLength { get; set; }

    [Benchmark]
    public byte[] DrawBytes()
    {
        var data = ConjectureData.ForGeneration(_rng.Split());
        return data.DrawBytes(ByteLength);
    }

    // --- Strategy<T> generation ---

    [Benchmark]
    public int IntegerStrategyNext()
    {
        var data = ConjectureData.ForGeneration(_rng.Split());
        return _integers.Next(data);
    }

    [Benchmark]
    public bool BooleanStrategyNext()
    {
        var data = ConjectureData.ForGeneration(_rng.Split());
        return _booleans.Next(data);
    }
}

// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using BenchmarkDotNet.Attributes;

using Conjecture.Core;

namespace Conjecture.Benchmarks;

/// <summary>Compares params-array vs params-span call sites for Strategy.OneOf.</summary>
[MemoryDiagnoser]
[SimpleJob]
public class OneOfBenchmarks
{
    private static readonly Strategy<int> S1 = Strategy.Just(1);
    private static readonly Strategy<int> S2 = Strategy.Just(2);
    private static readonly Strategy<int> S3 = Strategy.Just(3);
    private static readonly Strategy<int> S4 = Strategy.Just(4);
    private static readonly Strategy<int> S5 = Strategy.Just(5);
    private static readonly Strategy<int> S6 = Strategy.Just(6);
    private static readonly Strategy<int>[] ArrayOf3 = [S1, S2, S3];
    private static readonly Strategy<int>[] ArrayOf6 = [S1, S2, S3, S4, S5, S6];

    [Params(2, 3, 6)]
    public int ArgCount { get; set; }

    [Benchmark(Baseline = true)]
    public Strategy<int> ArrayOverload()
    {
        return ArgCount switch
        {
            2 => Strategy.OneOf(ArrayOf3[..2]),
            3 => Strategy.OneOf(ArrayOf3),
            _ => Strategy.OneOf(ArrayOf6),
        };
    }

    [Benchmark]
    public Strategy<int> SpanOverload()
    {
        return ArgCount switch
        {
            2 => Strategy.OneOf(S1, S2),
            3 => Strategy.OneOf(S1, S2, S3),
            _ => Strategy.OneOf(S1, S2, S3, S4, S5, S6),
        };
    }
}
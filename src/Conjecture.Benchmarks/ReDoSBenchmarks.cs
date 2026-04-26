// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Linq;

using BenchmarkDotNet.Attributes;

using Conjecture.Core;

using DotNetRegex = System.Text.RegularExpressions.Regex;

namespace Conjecture.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class ReDoSBenchmarks
{
    private static readonly DotNetRegex NestedQuantifier = new(@"(a+)+$");
    private static readonly DotNetRegex AlternationLoop = new(@"([a-zA-Z]+)*$");
    private static readonly DotNetRegex NestedAlternation = new(@"(a|aa)+$");

    [Params(5, 25, 100)]
    public int MaxMatchMs { get; set; }

    [Benchmark]
    public string? NestedQuantifier_FindPathologicalInput()
        => DataGen.Sample(Generate.ReDoSHunter(NestedQuantifier, MaxMatchMs), count: 1, seed: 42UL)
               .FirstOrDefault();

    [Benchmark]
    public string? AlternationLoop_FindPathologicalInput()
        => DataGen.Sample(Generate.ReDoSHunter(AlternationLoop, MaxMatchMs), count: 1, seed: 42UL)
               .FirstOrDefault();

    [Benchmark]
    public string? NestedAlternation_FindPathologicalInput()
        => DataGen.Sample(Generate.ReDoSHunter(NestedAlternation, MaxMatchMs), count: 1, seed: 42UL)
               .FirstOrDefault();
}
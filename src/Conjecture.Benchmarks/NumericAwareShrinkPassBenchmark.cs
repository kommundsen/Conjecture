// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using BenchmarkDotNet.Attributes;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Benchmarks;

/// <summary>
/// Measures NumericAwareShrinkPass overhead in the full shrink loop.
/// Baseline: plain-string property — pass does nothing (no digit runs).
/// Numeric: string with embedded number — pass actively reduces the segment.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class NumericAwareShrinkPassBenchmarks
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

    /// <summary>
    /// Property that fails on strings containing 'z'. No digit runs — NumericAwareShrinkPass
    /// scans all StringChar nodes and returns false immediately. Measures scan overhead only.
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task ShrinkPlainString()
    {
        await TestRunner.Run(settings, data =>
        {
            string s = Generate.Strings(minLength: 10, maxLength: 10).Generate(data);
            if (s.Contains('z')) { throw new Exception("fail"); }
        });
    }

    /// <summary>
    /// Property that fails when the last digit of the numeric string is not '0'.
    /// NumericAwareShrinkPass binary-searches the digit run toward '0', stopping at '1'
    /// (the minimal value that still fails). Measures the full numeric reduction path.
    /// </summary>
    [Benchmark]
    public async Task ShrinkNumericString()
    {
        Strategy<string> strategy = Generate.NumericStrings(minDigits: 1, maxDigits: 4);
        await TestRunner.Run(settings, data =>
        {
            string s = strategy.Generate(data);
            if (s[^1] > '0') { throw new Exception("fail"); }
        });
    }
}

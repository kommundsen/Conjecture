// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using BenchmarkDotNet.Attributes;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Benchmarks;

/// <summary>
/// Profiles the shrinker hot path across a range of failing-property shapes:
/// a tiny integer (IntegerReductionPass), a collection (DeleteBlocksPass /
/// IntervalDeletionPass), a command sequence (CommandSequenceShrinkPass), and
/// a CPU-bound property body (Amdahl check — property cost should dominate).
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class ShrinkerHotPathBenchmarks
{
    private ConjectureSettings settings = null!;
    private Strategy<StateMachineRun<int>> commandSequenceStrategy = null!;
    private Strategy<List<int>> collectionStrategy = null!;

    [GlobalSetup]
    public void Setup()
    {
        settings = new ConjectureSettings
        {
            Seed = 42UL,
            MaxExamples = 100,
            Database = false,
        };
        commandSequenceStrategy = Strategy.StateMachine<CounterMachineFailsAt3, int, CounterCommand>(maxSteps: 10);
        collectionStrategy = Strategy.Lists(Strategy.Integers<int>(), minSize: 20, maxSize: 20);
    }

    /// <summary>Fast-body baseline: a single integer failing at <c>&gt; 50</c>, exercising IntegerReductionPass.</summary>
    [Benchmark]
    public async Task ShrinkSmallInt()
    {
        await TestRunner.Run(settings, data =>
        {
            int n = Strategy.Integers<int>(0, 100).Generate(data);
            if (n > 50)
            {
                throw new Exception("fail");
            }
        });
    }

    /// <summary>Many IRNodes: a 20-element list failing when any element <c>&gt; 90</c>, exercising DeleteBlocksPass and IntervalDeletionPass.</summary>
    [Benchmark]
    public async Task ShrinkCollection()
    {
        await TestRunner.Run(settings, data =>
        {
            List<int> xs = collectionStrategy.Generate(data);
            foreach (int x in xs)
            {
                if (x > 90)
                {
                    throw new Exception("fail");
                }
            }
        });
    }

    /// <summary>Command sequence failing at step 3 of 10, exercising CommandSequenceShrinkPass.</summary>
    [Benchmark]
    public async Task ShrinkStateMachine_CommandSequence() =>
        await TestRunner.Run(settings, data => commandSequenceStrategy.Generate(data));

    /// <summary>Same shape as <see cref="ShrinkSmallInt"/> but with a CPU-bound property body (Amdahl check).</summary>
    [Benchmark]
    public async Task ShrinkWithSpinWait()
    {
        await TestRunner.Run(settings, data =>
        {
            int n = Strategy.Integers<int>(0, 100).Generate(data);
            Thread.SpinWait(1000);
            if (n > 50)
            {
                throw new Exception("fail");
            }
        });
    }
}
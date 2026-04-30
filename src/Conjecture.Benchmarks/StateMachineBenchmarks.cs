// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using BenchmarkDotNet.Attributes;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Benchmarks;

internal enum CounterCommand { Increment }

/// <summary>Counter machine that never violates its invariant (used for passing generation baseline).</summary>
internal sealed class CounterMachineNoFail : IStateMachine<int, CounterCommand>
{
    private static readonly Strategy<CounterCommand>[] AllCommands =
        [Strategy.Just(CounterCommand.Increment)];

    public int InitialState() => 0;
    public IEnumerable<Strategy<CounterCommand>> Commands(int state) => AllCommands;
    public int RunCommand(int state, CounterCommand cmd) => state + 1;
    public void Invariant(int state) { }
}

/// <summary>Counter machine whose invariant fails when count exceeds 3 (failure at step 3 of 10).</summary>
internal sealed class CounterMachineFailsAt3 : IStateMachine<int, CounterCommand>
{
    private static readonly Strategy<CounterCommand>[] AllCommands =
        [Strategy.Just(CounterCommand.Increment)];

    public int InitialState() => 0;
    public IEnumerable<Strategy<CounterCommand>> Commands(int state) => AllCommands;
    public int RunCommand(int state, CounterCommand cmd) => state + 1;
    public void Invariant(int state)
    {
        if (state > 3)
        {
            throw new InvalidOperationException($"counter exceeded 3: {state}");
        }
    }
}

/// <summary>Counter machine whose invariant fails when count exceeds 20 (failure at step 20 of 50).</summary>
internal sealed class CounterMachineFailsAt20 : IStateMachine<int, CounterCommand>
{
    private static readonly Strategy<CounterCommand>[] AllCommands =
        [Strategy.Just(CounterCommand.Increment)];

    public int InitialState() => 0;
    public IEnumerable<Strategy<CounterCommand>> Commands(int state) => AllCommands;
    public int RunCommand(int state, CounterCommand cmd) => state + 1;
    public void Invariant(int state)
    {
        if (state > 20)
        {
            throw new InvalidOperationException($"counter exceeded 20: {state}");
        }
    }
}

/// <summary>
/// Throughput and shrinking benchmarks for the stateful testing engine.
/// Inline counter-based machines isolate engine overhead from domain logic.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class StateMachineBenchmarks
{
    private SplittableRandom rng = null!;
    private Strategy<StateMachineRun<int>> passingStrategy = null!;
    private Strategy<StateMachineRun<int>> failingShortStrategy = null!;
    private Strategy<StateMachineRun<int>> failingLongStrategy = null!;
    private ConjectureSettings shrinkSettings = null!;

    [GlobalSetup]
    public void Setup()
    {
        rng = new SplittableRandom(42UL);
        passingStrategy = Strategy.StateMachine<CounterMachineNoFail, int, CounterCommand>(maxSteps: 50);
        failingShortStrategy = Strategy.StateMachine<CounterMachineFailsAt3, int, CounterCommand>(maxSteps: 10);
        failingLongStrategy = Strategy.StateMachine<CounterMachineFailsAt20, int, CounterCommand>(maxSteps: 50);
        shrinkSettings = new ConjectureSettings { Seed = 42UL, MaxExamples = 100, Database = false };
    }

    /// <summary>Throughput of generating 50-step sequences for a no-failure machine.</summary>
    [Benchmark]
    public StateMachineRun<int> StateMachineGeneration_Passing()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        return passingStrategy.Generate(data);
    }

    /// <summary>Throughput including failure-detection path; machine invariant fails when count exceeds 3.</summary>
    [Benchmark]
    public bool StateMachineGeneration_Failing()
    {
        ConjectureData data = ConjectureData.ForGeneration(rng.Split());
        try
        {
            failingShortStrategy.Generate(data);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Full find+shrink for a machine failing at step 3 of 10.</summary>
    [Benchmark]
    public async Task StateMachineShrinking_Short() =>
        await TestRunner.Run(shrinkSettings, data => failingShortStrategy.Generate(data));

    /// <summary>Full find+shrink for a machine failing at step 20 of 50.</summary>
    [Benchmark]
    public async Task StateMachineShrinking_Long() =>
        await TestRunner.Run(shrinkSettings, data => failingLongStrategy.Generate(data));
}
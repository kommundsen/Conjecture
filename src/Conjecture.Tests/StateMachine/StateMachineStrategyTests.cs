// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.StateMachine;

public class StateMachineStrategyTests
{
    // ─── Machines ─────────────────────────────────────────────────────────────

    private sealed class CounterMachine : IStateMachine<int, string>
    {
        public int InitialState() => 0;
        public IEnumerable<Strategy<string>> Commands(int state) => [Generate.Just("inc")];
        public int RunCommand(int state, string command) => state + 1;
        public void Invariant(int state) { }
    }

    private sealed class EmptyCommandMachine : IStateMachine<int, string>
    {
        public int InitialState() => 0;
        public IEnumerable<Strategy<string>> Commands(int state) => [];
        public int RunCommand(int state, string command) => state;
        public void Invariant(int state) { }
    }

    private sealed class AlwaysFailMachine : IStateMachine<int, string>
    {
        public int InitialState() => 0;
        public IEnumerable<Strategy<string>> Commands(int state) => [Generate.Just("cmd")];
        public int RunCommand(int state, string command) => state + 1;
        public void Invariant(int state) => throw new InvalidOperationException("always fails");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public void Strategy_ExtendsStrategyOfStateMachineRun()
    {
        Strategy<StateMachineRun<int>> strategy = new StateMachineStrategy<CounterMachine, int, string>(maxSteps: 5);
        Assert.NotNull(strategy);
    }

    [Fact]
    public void Generate_WithEmptyCommandMachine_ReturnsZeroSteps()
    {
        StateMachineStrategy<EmptyCommandMachine, int, string> strategy = new(maxSteps: 10);
        StateMachineRun<int> run = strategy.Generate(MakeData());
        Assert.Empty(run.Steps);
    }

    [Fact]
    public void Generate_WithEmptyCommandMachine_Passes()
    {
        StateMachineStrategy<EmptyCommandMachine, int, string> strategy = new(maxSteps: 10);
        StateMachineRun<int> run = strategy.Generate(MakeData());
        Assert.True(run.Passed);
    }

    [Fact]
    public void Generate_WithAlwaysFailInvariant_Throws()
    {
        StateMachineStrategy<AlwaysFailMachine, int, string> strategy = new(maxSteps: 10);
        Assert.ThrowsAny<Exception>(() => strategy.Generate(MakeData()));
    }

    [Fact]
    public void Generate_WithAlwaysFailInvariant_ExceptionMessageContainsStepIndex()
    {
        StateMachineStrategy<AlwaysFailMachine, int, string> strategy = new(maxSteps: 10);
        Exception ex = Assert.ThrowsAny<Exception>(() => strategy.Generate(MakeData()));
        Assert.Contains("step", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_WithMaxStepsZero_ReturnsZeroSteps()
    {
        StateMachineStrategy<CounterMachine, int, string> strategy = new(maxSteps: 0);
        StateMachineRun<int> run = strategy.Generate(MakeData());
        Assert.Empty(run.Steps);
    }

    [Fact]
    public void Generate_CommandLabel_IsToStringOfCommand()
    {
        StateMachineStrategy<CounterMachine, int, string> strategy = new(maxSteps: 5);
        StateMachineRun<int> run = strategy.Generate(MakeData());
        if (run.Steps.Count > 0)
        {
            // string formatter wraps in quotes; "inc".ToString() goes through FormatterRegistry
            Assert.Contains("inc", run.Steps[0].CommandLabel);
        }
    }

    [Fact]
    public void Generate_InsertsCommandStartSentinelBeforeEachStep()
    {
        StateMachineStrategy<CounterMachine, int, string> strategy = new(maxSteps: 5);
        ConjectureData data = MakeData();
        StateMachineRun<int> run = strategy.Generate(data);
        int sentinelCount = 0;
        foreach (IRNode node in data.IRNodes)
        {
            if (node.Kind == IRNodeKind.CommandStart)
            {
                sentinelCount++;
            }
        }
        Assert.Equal(run.Steps.Count, sentinelCount);
    }

    [Fact]
    public void Generate_CommandStartSentinelHasValueZero()
    {
        StateMachineStrategy<CounterMachine, int, string> strategy = new(maxSteps: 5);
        ConjectureData data = MakeData();
        strategy.Generate(data);
        foreach (IRNode node in data.IRNodes)
        {
            if (node.Kind == IRNodeKind.CommandStart)
            {
                Assert.Equal(0UL, node.Value);
            }
        }
    }

    [Fact]
    public void Generate_StepsAreInExecutionOrder()
    {
        StateMachineStrategy<CounterMachine, int, string> strategy = new(maxSteps: 5);
        StateMachineRun<int> run = strategy.Generate(MakeData());
        for (int i = 1; i < run.Steps.Count; i++)
        {
            Assert.True(run.Steps[i].State > run.Steps[i - 1].State);
        }
    }
}

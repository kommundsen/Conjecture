// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.StateMachine;

public class StateMachineRunnerTests
{
    private sealed class CounterMachine : IStateMachine<int, string>
    {
        public int InitialState() => 0;
        public IEnumerable<Strategy<string>> Commands(int state) => [Generate.Just("inc")];
        public int RunCommand(int state, string command) => state + 1;
        public void Invariant(int state) { }
    }

    private sealed class FailAtStepMachine(int failAtStep) : IStateMachine<int, string>
    {
        private int callCount;

        public int InitialState() => 0;
        public IEnumerable<Strategy<string>> Commands(int state) => [Generate.Just("inc")];
        public int RunCommand(int state, string command) => state + 1;

        public void Invariant(int state)
        {
            if (callCount++ == failAtStep)
            {
                throw new InvalidOperationException($"Invariant failed at step {failAtStep}.");
            }
        }
    }

    private sealed class NonStandardExceptionMachine : IStateMachine<int, string>
    {
        public int InitialState() => 0;
        public IEnumerable<Strategy<string>> Commands(int state) => [Generate.Just("cmd")];
        public int RunCommand(int state, string command) => state + 1;
        public void Invariant(int state) => throw new ArithmeticException("non-standard");
    }

    [Fact]
    public void Execute_EmptyCommandList_ReturnsPassed()
    {
        CounterMachine machine = new();
        StateMachineRun<int> run = StateMachineRunner.Execute(machine, []);
        Assert.True(run.Passed);
    }

    [Fact]
    public void Execute_EmptyCommandList_ReturnsZeroSteps()
    {
        CounterMachine machine = new();
        StateMachineRun<int> run = StateMachineRunner.Execute(machine, []);
        Assert.Empty(run.Steps);
    }

    [Fact]
    public void Execute_ThreePassingCommands_ReturnsPassed()
    {
        CounterMachine machine = new();
        StateMachineRun<int> run = StateMachineRunner.Execute(machine, ["inc", "inc", "inc"]);
        Assert.True(run.Passed);
    }

    [Fact]
    public void Execute_ThreePassingCommands_ReturnsThreeSteps()
    {
        CounterMachine machine = new();
        StateMachineRun<int> run = StateMachineRunner.Execute(machine, ["inc", "inc", "inc"]);
        Assert.Equal(3, run.Steps.Count);
    }

    [Fact]
    public void Execute_ThreePassingCommands_StepsHaveCorrectPostCommandStates()
    {
        CounterMachine machine = new();
        StateMachineRun<int> run = StateMachineRunner.Execute(machine, ["inc", "inc", "inc"]);
        Assert.Equal(new[] { 1, 2, 3 }, run.Steps.Select(s => s.State));
    }

    [Fact]
    public void Execute_InvariantFailsAtStepOne_DoesNotPass()
    {
        FailAtStepMachine machine = new(failAtStep: 1);
        StateMachineRun<int> run = StateMachineRunner.Execute(machine, ["inc", "inc", "inc"]);
        Assert.False(run.Passed);
    }

    [Fact]
    public void Execute_InvariantFailsAtStepOne_FailureStepIndexIsOne()
    {
        FailAtStepMachine machine = new(failAtStep: 1);
        StateMachineRun<int> run = StateMachineRunner.Execute(machine, ["inc", "inc", "inc"]);
        Assert.Equal(1, run.FailureStepIndex);
    }

    [Fact]
    public void Execute_InvariantFailsAtStepOne_StepsCountIsTwo()
    {
        FailAtStepMachine machine = new(failAtStep: 1);
        StateMachineRun<int> run = StateMachineRunner.Execute(machine, ["inc", "inc", "inc"]);
        Assert.Equal(2, run.Steps.Count);
    }

    [Fact]
    public void Execute_AnyExceptionTypeFromInvariant_TreatedAsFailure()
    {
        NonStandardExceptionMachine machine = new();
        StateMachineRun<int> run = StateMachineRunner.Execute(machine, ["cmd"]);
        Assert.False(run.Passed);
    }

    [Fact]
    public void Execute_CommandsAfterFailureNotExecuted()
    {
        int commandsExecuted = 0;
        FailAtStepMachine machine = new(failAtStep: 0);
        // Step 0 fails invariant; remaining two commands should not run
        StateMachineRun<int> run = StateMachineRunner.Execute(machine, ["inc", "inc", "inc"]);
        _ = commandsExecuted; // suppress warning
        Assert.Single(run.Steps);
    }
}

// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using Conjecture.Core;

namespace Conjecture.Tests.StateMachine;

public class IStateMachineTests
{
    private sealed class CounterMachine : IStateMachine<int, string>
    {
        public int InitialState() => 0;

        public IEnumerable<Strategy<string>> Commands(int state)
        {
            yield return Generate.Just("increment");
            if (state > 0)
            {
                yield return Generate.Just("decrement");
            }
        }

        public int RunCommand(int state, string command) => command switch
        {
            "increment" => state + 1,
            "decrement" => state - 1,
            _ => state
        };

        public void Invariant(int state)
        {
            if (state < 0)
            {
                throw new InvalidOperationException($"State must be non-negative, was {state}.");
            }
        }
    }

    private sealed class ListMachine : IStateMachine<List<int>, int>
    {
        public List<int> InitialState() => [];

        public IEnumerable<Strategy<int>> Commands(List<int> state) =>
            [Generate.Integers(1, 100)];

        public List<int> RunCommand(List<int> state, int command) { return [..state, command]; }

        public void Invariant(List<int> state) { }
    }

    private sealed class EmptyCommandMachine : IStateMachine<int, int>
    {
        public int InitialState() => 0;
        public IEnumerable<Strategy<int>> Commands(int state) => [];
        public int RunCommand(int state, int command) => state;
        public void Invariant(int state) { }
    }

    [Fact]
    public void CounterMachine_ImplementsInterface()
    {
        IStateMachine<int, string> machine = new CounterMachine();
        Assert.NotNull(machine);
    }

    [Fact]
    public void InitialState_ReturnsExpectedTState()
    {
        IStateMachine<int, string> machine = new CounterMachine();
        int state = machine.InitialState();
        Assert.Equal(0, state);
    }

    [Fact]
    public void Commands_ReturnsStrategiesForCurrentState()
    {
        IStateMachine<int, string> machine = new CounterMachine();
        IEnumerable<Strategy<string>> cmds = machine.Commands(0);
        Assert.NotEmpty(cmds);
    }

    [Fact]
    public void Commands_ReturnsEmptyEnumerable_WhenNoCommandsApplicable()
    {
        EmptyCommandMachine machine = new();
        IEnumerable<Strategy<int>> cmds = machine.Commands(0);
        Assert.Empty(cmds);
    }

    [Fact]
    public void RunCommand_ReturnsNewState()
    {
        IStateMachine<int, string> machine = new CounterMachine();
        int next = machine.RunCommand(0, "increment");
        Assert.Equal(1, next);
    }

    [Fact]
    public void Invariant_DoesNotThrow_ForValidState()
    {
        IStateMachine<int, string> machine = new CounterMachine();
        Exception? ex = Record.Exception(() => machine.Invariant(5));
        Assert.Null(ex);
    }

    [Fact]
    public void Invariant_Throws_ForInvalidState()
    {
        IStateMachine<int, string> machine = new CounterMachine();
        Assert.ThrowsAny<Exception>(() => machine.Invariant(-1));
    }

    [Fact]
    public void Interface_WorksWithReferenceTypeState()
    {
        IStateMachine<List<int>, int> machine = new ListMachine();
        List<int> initial = machine.InitialState();
        Assert.Empty(initial);
    }
}

// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.StateMachine;

public class GenStateMachineTests
{
    private sealed class CounterMachine : IStateMachine<int, string>
    {
        public int InitialState() => 0;
        public IEnumerable<Strategy<string>> Commands(int state) => [Generate.Just("inc")];
        public int RunCommand(int state, string command) => state + 1;
        public void Invariant(int state) { }
    }

    private static ConjectureData MakeData(ulong seed = 42UL) =>
        ConjectureData.ForGeneration(new SplittableRandom(seed));

    [Fact]
    public void StateMachine_ReturnsStrategyOfStateMachineRun()
    {
        Strategy<StateMachineRun<int>> strategy = Generate.StateMachine<CounterMachine, int, string>();
        Assert.NotNull(strategy);
    }

    [Fact]
    public void StateMachine_DefaultMaxSteps_GeneratesRun()
    {
        Strategy<StateMachineRun<int>> strategy = Generate.StateMachine<CounterMachine, int, string>();
        StateMachineRun<int> run = strategy.Generate(MakeData());
        Assert.NotNull(run);
    }

    [Fact]
    public void StateMachine_WithMaxStepsBound_StepsDoNotExceedBound()
    {
        Strategy<StateMachineRun<int>> strategy = Generate.StateMachine<CounterMachine, int, string>(maxSteps: 3);
        StateMachineRun<int> run = strategy.Generate(MakeData());
        Assert.True(run.Steps.Count <= 3);
    }

    [Fact]
    public void StateMachine_WithMaxStepsZero_ProducesEmptyRun()
    {
        Strategy<StateMachineRun<int>> strategy = Generate.StateMachine<CounterMachine, int, string>(maxSteps: 0);
        StateMachineRun<int> run = strategy.Generate(MakeData());
        Assert.Empty(run.Steps);
    }

    [Fact]
    public void StateMachine_ComposesWithGenerateCompose()
    {
        Strategy<int> derived = Generate.Compose(ctx =>
            ctx.Generate(Generate.StateMachine<CounterMachine, int, string>(maxSteps: 5)).Steps.Count);
        int count = derived.Generate(MakeData());
        Assert.True(count >= 0);
    }
}

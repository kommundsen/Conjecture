// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using Conjecture.Core;

namespace Conjecture.Tests.StateMachine;

public class StateMachineRunTests
{
    private static List<ExecutedStep<int>> TwoSteps() =>
    [
        new ExecutedStep<int>(10, "increment"),
        new ExecutedStep<int>(20, "increment"),
    ];

    [Fact]
    public void Constructor_AcceptsStepsAndNullFailureIndex()
    {
        StateMachineRun<int> run = new(TwoSteps(), initialState: 0, failureStepIndex: null);
        Assert.NotNull(run);
    }

    [Fact]
    public void Steps_ReturnsStepsInOrder()
    {
        StateMachineRun<int> run = new(TwoSteps(), initialState: 0, failureStepIndex: null);
        Assert.Equal(2, run.Steps.Count);
    }

    [Fact]
    public void Steps_FirstElement_IsFirstSuppliedStep()
    {
        StateMachineRun<int> run = new(TwoSteps(), initialState: 0, failureStepIndex: null);
        Assert.Equal(10, run.Steps[0].State);
    }

    [Fact]
    public void FailureStepIndex_IsNull_WhenNoViolation()
    {
        StateMachineRun<int> run = new(TwoSteps(), initialState: 0, failureStepIndex: null);
        Assert.Null(run.FailureStepIndex);
    }

    [Fact]
    public void FailureStepIndex_IsSet_WhenViolationOccurred()
    {
        StateMachineRun<int> run = new(TwoSteps(), initialState: 0, failureStepIndex: 1);
        Assert.Equal(1, run.FailureStepIndex);
    }

    [Fact]
    public void FinalState_IsLastStepState_WhenStepsNonEmpty()
    {
        StateMachineRun<int> run = new(TwoSteps(), initialState: 0, failureStepIndex: null);
        Assert.Equal(20, run.FinalState);
    }

    [Fact]
    public void FinalState_IsInitialState_WhenNoSteps()
    {
        StateMachineRun<int> run = new([], initialState: 42, failureStepIndex: null);
        Assert.Equal(42, run.FinalState);
    }

    [Fact]
    public void Passed_IsTrue_WhenFailureStepIndexIsNull()
    {
        StateMachineRun<int> run = new(TwoSteps(), initialState: 0, failureStepIndex: null);
        Assert.True(run.Passed);
    }

    [Fact]
    public void Passed_IsFalse_WhenFailureStepIndexIsSet()
    {
        StateMachineRun<int> run = new(TwoSteps(), initialState: 0, failureStepIndex: 0);
        Assert.False(run.Passed);
    }

    [Fact]
    public void ExecutedStep_HasState()
    {
        ExecutedStep<int> step = new(99, "cmd");
        Assert.Equal(99, step.State);
    }

    [Fact]
    public void ExecutedStep_HasCommandLabel()
    {
        ExecutedStep<int> step = new(0, "my-command");
        Assert.Equal("my-command", step.CommandLabel);
    }

    [Fact]
    public void ExecutedStep_IsValueEquatable()
    {
        ExecutedStep<int> a = new(5, "x");
        ExecutedStep<int> b = new(5, "x");
        Assert.Equal(a, b);
    }
}

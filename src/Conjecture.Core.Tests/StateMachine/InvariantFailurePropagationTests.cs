// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.StateMachine;

public class InvariantFailurePropagationTests
{
    // ─── Machines ─────────────────────────────────────────────────────────────

    private sealed class AlwaysFailMachine : IStateMachine<int, string>
    {
        public int InitialState() => 0;
        public IEnumerable<Strategy<string>> Commands(int state) => [Strategy.Just("cmd")];
        public int RunCommand(int state, string command) => state + 1;
        public void Invariant(int state) => throw new InvalidOperationException("always fails");
    }

    private sealed class NeverFailMachine : IStateMachine<int, string>
    {
        public int InitialState() => 0;
        public IEnumerable<Strategy<string>> Commands(int state) => [Strategy.Just("inc")];
        public int RunCommand(int state, string command) => state + 1;
        public void Invariant(int state) { }
    }

    private sealed class FailAfterTenMachine : IStateMachine<int, string>
    {
        public int InitialState() => 0;
        public IEnumerable<Strategy<string>> Commands(int state) => [Strategy.Just("inc")];
        public int RunCommand(int state, string command) => state + 1;
        public void Invariant(int state)
        {
            if (state > 10)
            {
                throw new InvalidOperationException("state exceeded 10");
            }
        }
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AlwaysFailMachine_TestRunnerDetectsFailure()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = new StateMachineStrategy<AlwaysFailMachine, int, string>(maxSteps: 5).Generate(data));
        Assert.False(result.Passed);
    }

    [Fact]
    public async Task NeverFailMachine_TestRunnerPasses()
    {
        ConjectureSettings settings = new() { MaxExamples = 20, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = new StateMachineStrategy<NeverFailMachine, int, string>(maxSteps: 5).Generate(data));
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task SometimesFailMachine_FoundWithinMaxExamples()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = new StateMachineStrategy<FailAfterTenMachine, int, string>(maxSteps: 20).Generate(data));
        Assert.False(result.Passed);
    }

    [Fact]
    public async Task FailureExceptionMessage_ContainsStepInfo()
    {
        ConjectureSettings settings = new() { MaxExamples = 10, UseDatabase = false };
        string? failureStackTrace = null;
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = new StateMachineStrategy<AlwaysFailMachine, int, string>(maxSteps: 5).Generate(data));
        _ = failureStackTrace;
        // Replay the counterexample and capture the thrown exception message
        Exception? thrown = null;
        ConjectureData replay = ConjectureData.ForRecord(result.Counterexample!);
        try
        {
            new StateMachineStrategy<AlwaysFailMachine, int, string>(maxSteps: 5).Generate(replay);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }
        Assert.NotNull(thrown);
        Assert.Contains("step", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Core.Internal;

using NUnit.Framework;

namespace Conjecture.NUnit.Tests.StateMachine;

internal sealed class AlwaysFailReportingMachine : IStateMachine<int, string>
{
    public int InitialState() => 0;
    public IEnumerable<Strategy<string>> Commands(int state) => [Strategy.Just("cmd")];
    public int RunCommand(int state, string command) => state + 1;
    public void Invariant(int state) => throw new InvalidOperationException("always fails");
}

internal sealed class FailsAtThreeReportingMachine : IStateMachine<int, string>
{
    public int InitialState() => 0;
    public IEnumerable<Strategy<string>> Commands(int state) => [Strategy.Just("step")];
    public int RunCommand(int state, string _) => state + 1;
    public void Invariant(int state)
    {
        if (state >= 3)
        {
            throw new InvalidOperationException($"state {state} exceeded");
        }
    }
}

[TestFixture]
public sealed class StateMachineReportingTests
{
    [Test]
    public void StatefulProperty_AfterStrategyInstantiation_FormatterIsRegisteredWithFormatterRegistry()
    {
        _ = new StateMachineStrategy<AlwaysFailReportingMachine, int, string>();

        IStrategyFormatter<StateMachineRun<int>>? formatter = FormatterRegistry.Get<StateMachineRun<int>>();
        Assert.That(formatter, Is.Not.Null);
    }

    [Test]
    public void StatefulProperty_FormatterOutput_ContainsStepSequenceAndFailsHereAnnotation()
    {
        _ = new StateMachineStrategy<AlwaysFailReportingMachine, int, string>();

        List<ExecutedStep<int>> steps = [new ExecutedStep<int>(1, "cmd")];
        StateMachineRun<int> run = new(steps, 0, 0);
        IStrategyFormatter<StateMachineRun<int>>? formatter = FormatterRegistry.Get<StateMachineRun<int>>();
        Assert.That(formatter, Is.Not.Null);
        string formatted = formatter!.Format(run);
        Assert.That(formatted, Does.Contain("state = InitialState();"));
        Assert.That(formatted, Does.Contain("// \u2190 fails here"));
    }

    [Test]
    public async Task StatefulProperty_FailureMessage_ContainsSeedReproductionLine()
    {
        TestRunResult result = await TestRunner.Run(
            new ConjectureSettings { MaxExamples = 1, Seed = 0xCAFEUL, UseDatabase = false },
            data => _ = Strategy.StateMachine<AlwaysFailReportingMachine, int, string>(maxSteps: 3).Generate(data));

        Assert.That(result.Passed, Is.False);
        string message = TestCaseHelper.BuildFailureMessage(result, []);
        Assert.That(message, Does.Contain("Reproduce with: [Property(Seed = 0xCAFE)]"));
    }

    [Test]
    public async Task StatefulProperty_ShrinkCount_IsReportedInResult()
    {
        TestRunResult result = await TestRunner.Run(
            new ConjectureSettings { MaxExamples = 50, Seed = 1UL, UseDatabase = false },
            data => _ = Strategy.StateMachine<FailsAtThreeReportingMachine, int, string>(maxSteps: 20).Generate(data));

        Assert.That(result.Passed, Is.False);
        Assert.That(result.ShrinkCount, Is.GreaterThan(0), $"Expected shrink count > 0, got {result.ShrinkCount}.");
    }
}
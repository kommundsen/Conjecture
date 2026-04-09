// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;

namespace Conjecture.Core.Tests.StateMachine;

public class StateMachineFormatterTests
{
    private static StateMachineRun<int> MakeRun(int? failAt, params string[] labels)
    {
        List<ExecutedStep<int>> steps = labels
            .Select((label, i) => new ExecutedStep<int>(i + 1, label))
            .ToList();
        return new StateMachineRun<int>(steps, 0, failAt);
    }

    [Fact]
    public void Format_PassingRun_ReturnsNeutralPlaceholder()
    {
        StateMachineFormatter<int> formatter = new();
        StateMachineRun<int> run = MakeRun(null);

        string result = formatter.Format(run);

        Assert.False(string.IsNullOrEmpty(result));
        // Passing runs don't have a failure annotation.
        Assert.DoesNotContain("fails here", result);
    }

    [Fact]
    public void Format_FailingRun_StartsWithInitialStateDeclaration()
    {
        StateMachineFormatter<int> formatter = new();
        StateMachineRun<int> run = MakeRun(0, "Push(1)");

        string result = formatter.Format(run);

        Assert.Contains("state = InitialState();", result);
    }

    [Fact]
    public void Format_FailingRun_ContainsRunCommandForEachStep()
    {
        StateMachineFormatter<int> formatter = new();
        StateMachineRun<int> run = MakeRun(1, "Push(1)", "Push(2)");

        string result = formatter.Format(run);

        Assert.Contains("RunCommand(state, Push(1));", result);
        Assert.Contains("RunCommand(state, Push(2));", result);
    }

    [Fact]
    public void Format_FailingRun_InvariantAnnotationAtFailureStep()
    {
        StateMachineFormatter<int> formatter = new();
        StateMachineRun<int> run = MakeRun(1, "Push(1)", "Push(2)");

        string result = formatter.Format(run);

        // After the failing step (index 1), there should be an invariant annotation.
        Assert.Contains("Invariant(state); // ← fails here", result);
    }

    [Fact]
    public void Format_FailingRun_InvariantAnnotationAfterFailingStep()
    {
        StateMachineFormatter<int> formatter = new();
        // Fails at step index 0 (first step), so invariant annotation is after Push(1)
        StateMachineRun<int> run = MakeRun(0, "Push(1)", "Push(2)");

        string result = formatter.Format(run);

        string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Find the line with fails here
        int failLine = Array.FindIndex(lines, l => l.Contains("fails here"));
        int pushLine = Array.FindIndex(lines, l => l.Contains("RunCommand(state, Push(1))"));
        // Invariant annotation comes after the failing step's RunCommand
        Assert.True(failLine > pushLine, "Invariant annotation must appear after the failing RunCommand.");
        // Step 2 (Push(2)) must NOT appear — commands after failure are not in the run
        Assert.DoesNotContain("Push(2)", result);
    }

    [Fact]
    public void Format_EmptyPassingRun_ReturnsPlaceholder()
    {
        StateMachineFormatter<int> formatter = new();
        StateMachineRun<int> run = MakeRun(null);

        string result = formatter.Format(run);

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void Formatter_IsRegisteredWithFormatterRegistry_AfterConstruction()
    {
        // Creating the formatter and registering it should make FormatterRegistry.Get<> return it.
        StateMachineFormatter<int> formatter = new();
        FormatterRegistry.Register(formatter);

        IStrategyFormatter<StateMachineRun<int>>? registered = FormatterRegistry.Get<StateMachineRun<int>>();

        Assert.NotNull(registered);
    }
}
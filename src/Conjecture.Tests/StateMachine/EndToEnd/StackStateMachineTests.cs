// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Tests.StateMachine.EndToEnd;

/// <summary>
/// End-to-end tests verifying that stateful testing finds, shrinks, and reports
/// invariant violations in a model-based stack implementation.
/// </summary>
public class StackStateMachineTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static int CountCommandStarts(IReadOnlyList<IRNode> nodes) =>
        nodes.Count(n => n.Kind == IRNodeKind.CommandStart);

    // ─── State ────────────────────────────────────────────────────────────────

    private readonly record struct StackState(Stack<int> Stack, int ModelCount);

    // ─── Commands ─────────────────────────────────────────────────────────────

    private abstract class StackCommand
    {
        public sealed class Push : StackCommand
        {
            public Push(int value) { Value = value; }
            public int Value { get; }
            public override string ToString() => $"Push({Value})";
        }

        public sealed class Pop : StackCommand
        {
            public override string ToString() => "Pop()";
        }
    }

    // ─── Machines ─────────────────────────────────────────────────────────────

    // Commands always returns [Push, Pop] regardless of state. Pop on an empty stack is a
    // no-op so that [Pop]-alone does not trigger the invariant — ensuring [Push, Pop] is the
    // true minimal failing sequence and the shrinker cannot escape to a stale-index failure.
    private sealed class BuggyStackMachine : IStateMachine<StackState, StackCommand>
    {
        public StackState InitialState() => new(new Stack<int>(), ModelCount: 0);

        public IEnumerable<Strategy<StackCommand>> Commands(StackState state)
        {
            yield return Generate.Integers<int>(0, 9).Select(n => (StackCommand)new StackCommand.Push(n));
            yield return Generate.Just((StackCommand)new StackCommand.Pop());
        }

        public StackState RunCommand(StackState state, StackCommand cmd)
        {
            if (cmd is StackCommand.Push push)
            {
                Stack<int> pushed = new(state.Stack.Reverse());
                pushed.Push(push.Value);
                return new(pushed, state.ModelCount + 1);
            }
            if (state.Stack.Count == 0)
            {
                return state; // Pop on empty is a no-op; keeps invariant satisfied.
            }
            // Planted bug: decrements ModelCount by 2 instead of 1.
            Stack<int> popped = new(state.Stack.Reverse());
            popped.Pop();
            return new(popped, state.ModelCount - 2);
        }

        public void Invariant(StackState state)
        {
            if (state.ModelCount != state.Stack.Count)
                throw new InvalidOperationException(
                    $"ModelCount {state.ModelCount} != Stack.Count {state.Stack.Count}");
        }
    }

    private sealed class CorrectStackMachine : IStateMachine<StackState, StackCommand>
    {
        public StackState InitialState() => new(new Stack<int>(), ModelCount: 0);

        public IEnumerable<Strategy<StackCommand>> Commands(StackState state)
        {
            yield return Generate.Integers<int>(0, 9).Select(n => (StackCommand)new StackCommand.Push(n));
            yield return Generate.Just((StackCommand)new StackCommand.Pop());
        }

        public StackState RunCommand(StackState state, StackCommand cmd)
        {
            if (cmd is StackCommand.Push push)
            {
                Stack<int> pushed = new(state.Stack.Reverse());
                pushed.Push(push.Value);
                return new(pushed, state.ModelCount + 1);
            }
            if (state.Stack.Count == 0)
            {
                return state;
            }
            Stack<int> popped = new(state.Stack.Reverse());
            popped.Pop();
            return new(popped, state.ModelCount - 1);
        }

        public void Invariant(StackState state)
        {
            if (state.ModelCount != state.Stack.Count)
                throw new InvalidOperationException(
                    $"ModelCount {state.ModelCount} != Stack.Count {state.Stack.Count}");
        }
    }

    // Uses state-dependent Commands (Pop only when Count > 0) to exercise the engine's
    // state-aware command selection. No bug — used for the availability test only.
    private sealed class StateAwareStackMachine : IStateMachine<StackState, StackCommand>
    {
        public StackState InitialState() => new(new Stack<int>(), ModelCount: 0);

        public IEnumerable<Strategy<StackCommand>> Commands(StackState state)
        {
            yield return Generate.Integers<int>(0, 9).Select(n => (StackCommand)new StackCommand.Push(n));
            if (state.Stack.Count > 0)
                yield return Generate.Just((StackCommand)new StackCommand.Pop());
        }

        public StackState RunCommand(StackState state, StackCommand cmd)
        {
            if (cmd is StackCommand.Push push)
            {
                Stack<int> pushed = new(state.Stack.Reverse());
                pushed.Push(push.Value);
                return new(pushed, state.ModelCount + 1);
            }
            // Pop: Stack.Count > 0 guaranteed by Commands.
            Stack<int> popped = new(state.Stack.Reverse());
            popped.Pop();
            return new(popped, state.ModelCount - 1);
        }

        public void Invariant(StackState state)
        {
            if (state.ModelCount != state.Stack.Count)
                throw new InvalidOperationException(
                    $"ModelCount {state.ModelCount} != Stack.Count {state.Stack.Count}");
        }
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuggyStack_WithPlantedBug_FindsFailureWithin100Examples()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = Generate.StateMachine<BuggyStackMachine, StackState, StackCommand>().Generate(data));
        Assert.False(result.Passed);
    }

    [Fact]
    public async Task BuggyStack_ShrunkCounterexample_IsMinimal()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = Generate.StateMachine<BuggyStackMachine, StackState, StackCommand>().Generate(data));
        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        Assert.True(result.ShrinkCount > 0, $"Expected shrinks but got ShrinkCount={result.ShrinkCount}");
    }

    [Fact]
    public async Task BuggyStack_ShrunkCounterexample_HasExactlyTwoCommands()
    {
        // Minimal failing sequence is [Push, Pop]: Push makes the stack non-empty,
        // then the buggy Pop decrements ModelCount by 2, violating the invariant.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = Generate.StateMachine<BuggyStackMachine, StackState, StackCommand>().Generate(data));
        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        Assert.Equal(2, CountCommandStarts(result.Counterexample!));
    }

    [Fact]
    public async Task CorrectStack_NoBug_PassesAllExamples()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 3UL, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = Generate.StateMachine<CorrectStackMachine, StackState, StackCommand>().Generate(data));
        Assert.True(result.Passed);
        Assert.Null(result.Counterexample);
    }

    [Fact]
    public async Task StateDependentCommands_PopOnlyGeneratedWhenStackNonEmpty()
    {
        // StateAwareStackMachine only yields Pop when Stack.Count > 0. If the engine ever
        // selected Pop when Count == 0, Stack.Pop() would throw, causing a failure. A passing
        // result confirms state-dependent command availability is respected.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 2UL, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = Generate.StateMachine<StateAwareStackMachine, StackState, StackCommand>().Generate(data));
        Assert.True(result.Passed);
    }
}

// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

using Conjecture.Core;
using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.StateMachine.EndToEnd;

/// <summary>
/// End-to-end tests verifying that stateful testing finds, shrinks, and reports
/// invariant violations in a model-based queue implementation.
/// </summary>
public class QueueStateMachineTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static int CountCommandStarts(IReadOnlyList<IRNode> nodes) =>
        nodes.Count(n => n.Kind == IRNodeKind.CommandStart);

    // ─── State ────────────────────────────────────────────────────────────────

    // SnapshotPeek holds the value captured by the last Peek command; null when
    // no Peek has been executed since the last enqueue/dequeue.
    private readonly record struct QueueState(Queue<int> Queue, int? SnapshotPeek);

    // ─── Commands ─────────────────────────────────────────────────────────────

    private abstract class QueueCommand
    {
        public sealed class Enqueue(int value) : QueueCommand
        {
            public int Value { get; } = value; public override string ToString() => $"Enqueue({Value})";
        }

        public sealed class Dequeue : QueueCommand
        {
            public override string ToString() => "Dequeue()";
        }

        public sealed class Peek : QueueCommand
        {
            public override string ToString() => "Peek()";
        }
    }

    // ─── Machines ─────────────────────────────────────────────────────────────

    // Dequeue/Peek are no-ops when the queue is empty, so empty-queue paths do
    // not trigger the invariant.
    // Planted bug: when Count > 1, Peek records Queue.Peek() + 1 (off-by-one)
    // instead of Queue.Peek(). This is value-independent: even with all values
    // shrunk to 0, [Enqueue(0), Enqueue(0), Peek] yields snapshot=1 ≠ front=0.
    // A single-element queue uses the correct path, so [Enqueue, Peek] alone
    // cannot trigger the invariant.
    private sealed class BuggyQueueMachine : IStateMachine<QueueState, QueueCommand>
    {
        public QueueState InitialState() => new(new Queue<int>(), null);

        public IEnumerable<Strategy<QueueCommand>> Commands(QueueState state)
        {
            yield return Strategy.Integers<int>(0, 9).Select(n => (QueueCommand)new QueueCommand.Enqueue(n));
            yield return Strategy.Just((QueueCommand)new QueueCommand.Dequeue());
            yield return Strategy.Just((QueueCommand)new QueueCommand.Peek());
        }

        public QueueState RunCommand(QueueState state, QueueCommand cmd)
        {
            if (cmd is QueueCommand.Enqueue enqueue)
            {
                Queue<int> q = new(state.Queue);
                q.Enqueue(enqueue.Value);
                return new(q, null);
            }

            if (cmd is QueueCommand.Dequeue)
            {
                if (state.Queue.Count == 0)
                {
                    return new(state.Queue, null);
                }

                Queue<int> q = new(state.Queue);
                q.Dequeue();
                return new(q, null);
            }

            // Peek — planted bug: off-by-one on front element when Count > 1.
            if (state.Queue.Count == 0)
            {
                return state;
            }

            int peeked = state.Queue.Count > 1
                ? state.Queue.Peek() + 1   // Bug: returns front + 1, not front
                : state.Queue.Peek();       // Correct when only one element
            return new(new Queue<int>(state.Queue), peeked);
        }

        public void Invariant(QueueState state)
        {
            if (state.SnapshotPeek.HasValue && state.Queue.Count > 0
                && state.SnapshotPeek.Value != state.Queue.Peek())
            {
                throw new InvalidOperationException(
                    $"Peek returned {state.SnapshotPeek.Value} but front element is {state.Queue.Peek()}");
            }
        }
    }

    private sealed class CorrectQueueMachine : IStateMachine<QueueState, QueueCommand>
    {
        public QueueState InitialState() => new(new Queue<int>(), null);

        public IEnumerable<Strategy<QueueCommand>> Commands(QueueState state)
        {
            yield return Strategy.Integers<int>(0, 9).Select(n => (QueueCommand)new QueueCommand.Enqueue(n));
            yield return Strategy.Just((QueueCommand)new QueueCommand.Dequeue());
            yield return Strategy.Just((QueueCommand)new QueueCommand.Peek());
        }

        public QueueState RunCommand(QueueState state, QueueCommand cmd)
        {
            if (cmd is QueueCommand.Enqueue enqueue)
            {
                Queue<int> q = new(state.Queue);
                q.Enqueue(enqueue.Value);
                return new(q, null);
            }

            if (cmd is QueueCommand.Dequeue)
            {
                if (state.Queue.Count == 0)
                {
                    return new(state.Queue, null);
                }

                Queue<int> q = new(state.Queue);
                q.Dequeue();
                return new(q, null);
            }

            // Peek — correct implementation.
            return state.Queue.Count == 0 ? state : new(new Queue<int>(state.Queue), state.Queue.Peek());
        }

        public void Invariant(QueueState state)
        {
            if (state.SnapshotPeek.HasValue && state.Queue.Count > 0
                && state.SnapshotPeek.Value != state.Queue.Peek())
            {
                throw new InvalidOperationException(
                    $"Peek returned {state.SnapshotPeek.Value} but front element is {state.Queue.Peek()}");
            }
        }
    }

    // Only yields Dequeue/Peek when Count > 0. If the engine ever selected them
    // on an empty queue, Queue.Dequeue()/Queue.Peek() would throw, causing a
    // test failure. A passing result confirms state-dependent availability works.
    private sealed class StateAwareQueueMachine : IStateMachine<QueueState, QueueCommand>
    {
        public QueueState InitialState() => new(new Queue<int>(), null);

        public IEnumerable<Strategy<QueueCommand>> Commands(QueueState state)
        {
            yield return Strategy.Integers<int>(0, 9).Select(n => (QueueCommand)new QueueCommand.Enqueue(n));
            if (state.Queue.Count > 0)
            {
                yield return Strategy.Just((QueueCommand)new QueueCommand.Dequeue());
                yield return Strategy.Just((QueueCommand)new QueueCommand.Peek());
            }
        }

        public QueueState RunCommand(QueueState state, QueueCommand cmd)
        {
            if (cmd is QueueCommand.Enqueue enqueue)
            {
                Queue<int> q = new(state.Queue);
                q.Enqueue(enqueue.Value);
                return new(q, null);
            }

            if (cmd is QueueCommand.Dequeue)
            {
                Queue<int> q = new(state.Queue);
                q.Dequeue(); // throws if empty — proves state-dependent selection works
                return new(q, null);
            }

            // Peek — throws if empty, proving state-dependent selection works.
            return new(new Queue<int>(state.Queue), state.Queue.Peek());
        }

        public void Invariant(QueueState state)
        {
            if (state.SnapshotPeek.HasValue && state.Queue.Count > 0
                && state.SnapshotPeek.Value != state.Queue.Peek())
            {
                throw new InvalidOperationException(
                    $"Peek returned {state.SnapshotPeek.Value} but front element is {state.Queue.Peek()}");
            }
        }
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuggyQueue_WithPlantedBug_FindsFailureWithin100Examples()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = Strategy.StateMachine<BuggyQueueMachine, QueueState, QueueCommand>().Generate(data));
        Assert.False(result.Passed);
    }

    [Fact]
    public async Task BuggyQueue_ShrunkCounterexample_IsMinimal()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = Strategy.StateMachine<BuggyQueueMachine, QueueState, QueueCommand>().Generate(data));
        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        Assert.True(result.ShrinkCount > 0, $"Expected shrinks but got ShrinkCount={result.ShrinkCount}");
    }

    [Fact]
    public async Task BuggyQueue_ShrunkCounterexample_HasExactlyThreeCommands()
    {
        // Minimal failing sequence is [Enqueue(0), Enqueue(0), Peek]:
        // the buggy Peek records Queue.Peek() + 1 = 1 when Count > 1, but the
        // actual front is 0, so 1 ≠ 0. This is value-independent — both values
        // shrink to 0 yet the bug is still detectable.
        // A single-element queue is unaffected (bug only fires when Count > 1),
        // so no 2-step sequence can trigger the invariant.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 1UL, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = Strategy.StateMachine<BuggyQueueMachine, QueueState, QueueCommand>().Generate(data));
        Assert.False(result.Passed);
        Assert.NotNull(result.Counterexample);
        Assert.Equal(3, CountCommandStarts(result.Counterexample!));
    }

    [Fact]
    public async Task CorrectQueue_NoBug_PassesAllExamples()
    {
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 3UL, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = Strategy.StateMachine<CorrectQueueMachine, QueueState, QueueCommand>().Generate(data));
        Assert.True(result.Passed);
        Assert.Null(result.Counterexample);
    }

    [Fact]
    public async Task StateDependentCommands_DequeueNeverSelectedWhenQueueEmpty()
    {
        // StateAwareQueueMachine only yields Dequeue/Peek when Count > 0. A passing
        // result confirms the engine respects state-dependent command availability.
        ConjectureSettings settings = new() { MaxExamples = 100, Seed = 2UL, UseDatabase = false };
        TestRunResult result = await TestRunner.Run(settings,
            data => _ = Strategy.StateMachine<StateAwareQueueMachine, QueueState, QueueCommand>().Generate(data));
        Assert.True(result.Passed);
    }
}
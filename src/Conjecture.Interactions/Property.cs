// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Core.Internal;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Core;

/// <summary>
/// Imperative entry point for property-based testing. All framework adapters and
/// interactive notebooks delegate to these methods.
/// </summary>
#pragma warning disable RS0026 // Do not add multiple overloads with optional parameters
public static class Property
{
    /// <summary>
    /// Runs a property test using <paramref name="strategy"/> to generate values and
    /// <paramref name="assertion"/> to check them against <paramref name="target"/>.
    /// </summary>
    /// <typeparam name="T">The type of value under test.</typeparam>
    /// <param name="target">The interaction target passed to each assertion invocation.</param>
    /// <param name="strategy">Strategy used to generate test inputs.</param>
    /// <param name="assertion">The property body; receives the target and a generated value.</param>
    /// <param name="settings">Optional settings override. When <see langword="null"/>, defaults are used.</param>
    /// <param name="ct">Cancellation token. Checked before test execution begins.</param>
    /// <exception cref="ConjectureException">Thrown when a falsifying example is found.</exception>
    public static async Task ForAll<T>(
        IInteractionTarget target,
        Strategy<T> strategy,
        Func<IInteractionTarget, T, Task> assertion,
        ConjectureSettings? settings = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(assertion);

        ct.ThrowIfCancellationRequested();

        ConjectureSettings resolved = settings ?? new ConjectureSettings();

        TestRunResult result = await TestRunner.RunAsync(resolved, async data =>
        {
            ct.ThrowIfCancellationRequested();
            T value = strategy.Generate(data);
            await assertion(target, value);
        });

        if (!result.Passed)
        {
            string message = BuildStrategyFailureMessage(result, strategy);
            throw new ConjectureException(message);
        }
    }

    /// <summary>
    /// Runs a stateful property test using <paramref name="machine"/> to drive the system
    /// and <paramref name="target"/> to execute interactions.
    /// </summary>
    /// <typeparam name="TState">The state type of the machine.</typeparam>
    /// <param name="target">The interaction target passed to each command execution.</param>
    /// <param name="machine">The state machine that models expected system behaviour.</param>
    /// <param name="settings">Optional settings override. When <see langword="null"/>, defaults are used.</param>
    /// <param name="ct">Cancellation token. Checked before test execution begins.</param>
    /// <exception cref="ConjectureException">Thrown when an invariant violation is found.</exception>
    public static async Task ForAll<TState>(
        IInteractionTarget target,
        InteractionStateMachine<TState> machine,
        ConjectureSettings? settings = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(machine);

        ct.ThrowIfCancellationRequested();

        ConjectureSettings resolved = settings ?? new ConjectureSettings();

        TestRunResult result = await TestRunner.RunAsync(resolved, data =>
        {
            ct.ThrowIfCancellationRequested();

            TState state = machine.InitialState();
            int length = (int)data.NextInteger(0, 50);

            for (int i = 0; i < length; i++)
            {
                Strategy<IInteraction>[] commands = [.. machine.Commands(state)];
                if (commands.Length == 0)
                {
                    break;
                }

                data.InsertCommandStart();
                IInteraction command = new OneOfStrategy<IInteraction>(commands).Generate(data);
                state = machine.RunCommand(state, command, target, ct);
                machine.Invariant(state);
            }

            return Task.CompletedTask;
        });

        if (!result.Passed)
        {
            string message = result.FailureStackTrace ?? "State machine invariant violated.";
            throw new ConjectureException(message);
        }
    }

    private static string BuildStrategyFailureMessage<T>(TestRunResult result, Strategy<T> strategy)
    {
        IReadOnlyList<IRNode> counterexample = result.Counterexample!;
        ConjectureData replay = ConjectureData.ForRecord(counterexample);
        T shrunkValue = strategy.Generate(replay);
        string valueLabel = strategy.Label ?? "value";
        IEnumerable<(string name, object value)> pairs = [(valueLabel, (object)shrunkValue!)];
        return CounterexampleFormatter.Format(pairs, result.Seed!.Value, result.ExampleCount, result.ShrinkCount, result.TargetingScores);
    }
}
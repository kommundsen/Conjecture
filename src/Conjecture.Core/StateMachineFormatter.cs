// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Text;

using Conjecture.Abstractions.Strategies;

namespace Conjecture.Core;

/// <summary>
/// Formats a <see cref="StateMachineRun{TState}"/> counterexample as a human-readable
/// step-sequence showing the exact commands that led to an invariant violation.
/// </summary>
/// <typeparam name="TState">The type representing the system's state.</typeparam>
public sealed class StateMachineFormatter<TState> : IStrategyFormatter<StateMachineRun<TState>>
{
    /// <inheritdoc/>
    public string Format(StateMachineRun<TState> value)
    {
        if (value.Passed)
        {
            return $"StateMachineRun (passed, {value.Steps.Count} steps)";
        }

        int failAt = value.FailureStepIndex!.Value;
        StringBuilder sb = new();
        sb.AppendLine("state = InitialState();");
        for (int i = 0; i < failAt && i < value.Steps.Count; i++)
        {
            sb.AppendLine($"RunCommand(state, {value.Steps[i].CommandLabel});");
        }
        if (failAt < value.Steps.Count)
        {
            sb.AppendLine($"RunCommand(state, {value.Steps[failAt].CommandLabel});");
        }
        sb.Append("Invariant(state); // \u2190 fails here");
        return sb.ToString();
    }
}
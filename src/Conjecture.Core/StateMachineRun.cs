// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;

namespace Conjecture.Core;

/// <summary>
/// Represents the result of a single stateful test run, containing the sequence of executed
/// steps, the final state, and information about any invariant violation that occurred.
/// </summary>
/// <typeparam name="TState">The type representing the system's state.</typeparam>
public sealed class StateMachineRun<TState>(
    IReadOnlyList<ExecutedStep<TState>> steps,
    TState initialState,
    int? failureStepIndex)
{
    /// <summary>Gets the sequence of executed steps in the order they were applied.</summary>
    public IReadOnlyList<ExecutedStep<TState>> Steps { get; } = steps;

    /// <summary>
    /// Gets the 0-based index of the step at which an invariant violation was detected,
    /// or <see langword="null"/> if the run completed without a violation.
    /// </summary>
    public int? FailureStepIndex { get; } = failureStepIndex;

    /// <summary>
    /// Gets the state after the last executed step, or the initial state when no steps were executed.
    /// </summary>
    public TState FinalState => Steps.Count > 0 ? Steps[^1].State : initialState;

    /// <summary>
    /// Gets a value indicating whether the run completed without an invariant violation.
    /// </summary>
    public bool Passed => FailureStepIndex is null;
}

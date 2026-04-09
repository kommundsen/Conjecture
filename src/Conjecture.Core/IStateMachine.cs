// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Collections.Generic;

namespace Conjecture.Core;

/// <summary>
/// Defines a stateful system under test for use with Conjecture's stateful testing engine.
/// </summary>
/// <typeparam name="TState">The type representing the system's state.</typeparam>
/// <typeparam name="TCommand">The type representing a command that can be applied to the state.</typeparam>
public interface IStateMachine<TState, TCommand>
{
    /// <summary>Returns the initial state of the system before any commands have been applied.</summary>
    TState InitialState();

    /// <summary>
    /// Returns the commands that are applicable to the given <paramref name="state"/>.
    /// An empty enumerable signals that no further commands can be applied.
    /// </summary>
    /// <param name="state">The current state of the system.</param>
    IEnumerable<Strategy<TCommand>> Commands(TState state);

    /// <summary>
    /// Applies <paramref name="command"/> to <paramref name="state"/> and returns the resulting state.
    /// The original state must not be mutated; return a new state value instead.
    /// </summary>
    /// <param name="state">The current state of the system.</param>
    /// <param name="command">The command to apply.</param>
    TState RunCommand(TState state, TCommand command);

    /// <summary>
    /// Asserts that <paramref name="state"/> satisfies all invariants of the system.
    /// Throw any exception to signal a violation; return normally to indicate the state is valid.
    /// </summary>
    /// <param name="state">The state to validate.</param>
    void Invariant(TState state);
}
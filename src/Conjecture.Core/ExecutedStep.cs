// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core;

/// <summary>
/// Represents a single executed step in a stateful test run, capturing the resulting
/// state and a human-readable label for the command that was applied.
/// </summary>
/// <typeparam name="TState">The type representing the system's state.</typeparam>
/// <param name="State">The post-command state after this step was executed.</param>
/// <param name="CommandLabel">A human-readable label for the command that produced this state.</param>
public readonly record struct ExecutedStep<TState>(TState State, string CommandLabel);

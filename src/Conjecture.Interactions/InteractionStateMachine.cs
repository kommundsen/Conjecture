// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conjecture.Core;

namespace Conjecture.Interactions;

/// <summary>
/// Base class for stateful interaction-based property tests.
/// Subclass this and implement <see cref="InitialState"/>, <see cref="Commands"/>,
/// <see cref="RunCommand(TState, IInteraction, IInteractionTarget, CancellationToken)"/>,
/// and <see cref="Invariant"/> to describe the system under test.
/// </summary>
/// <typeparam name="TState">The type representing the system's state.</typeparam>
public abstract class InteractionStateMachine<TState> : IStateMachine<TState, IInteraction>
{
    /// <inheritdoc />
    public abstract TState InitialState();

    /// <inheritdoc />
    public abstract IEnumerable<Strategy<IInteraction>> Commands(TState state);

    /// <summary>
    /// Applies <paramref name="interaction"/> to <paramref name="state"/> by dispatching
    /// via <paramref name="target"/> and returns the resulting state.
    /// </summary>
    /// <param name="state">The current state of the system.</param>
    /// <param name="interaction">The interaction to dispatch.</param>
    /// <param name="target">The target that executes the interaction.</param>
    /// <param name="ct">Cancellation token propagated to <see cref="IInteractionTarget.ExecuteAsync"/>.</param>
    public abstract TState RunCommand(
        TState state,
        IInteraction interaction,
        IInteractionTarget target,
        CancellationToken ct);

    /// <inheritdoc />
    public abstract void Invariant(TState state);

    TState IStateMachine<TState, IInteraction>.RunCommand(TState state, IInteraction command)
    {
        return RunCommand(state, command, NoOpInteractionTarget.Instance, CancellationToken.None);
    }

    private sealed class NoOpInteractionTarget : IInteractionTarget
    {
        internal static readonly NoOpInteractionTarget Instance = new();

        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            return Task.FromResult<object?>(null);
        }
    }
}

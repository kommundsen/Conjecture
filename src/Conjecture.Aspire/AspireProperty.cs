// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Threading;
using System.Threading.Tasks;

using Conjecture.Core;

using Conjecture.Abstractions.Aspire;

namespace Conjecture.Aspire;

/// <summary>Entry point for running Aspire stateful property tests.</summary>
public static class AspireProperty
{
    /// <summary>
    /// Runs a stateful property test against an Aspire distributed application.
    /// </summary>
    /// <typeparam name="TState">The state type used by the state machine.</typeparam>
    /// <param name="fixture">The fixture that owns the distributed application lifecycle.</param>
    /// <param name="machine">The state machine describing commands and invariants.</param>
    /// <param name="settings">Settings controlling example count and seed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task RunAsync<TState>(
        IAspireAppFixture fixture,
        AspireStateMachine<TState> machine,
        ConjectureSettings settings,
        CancellationToken cancellationToken)
        => AspirePropertyRunner.RunAsync(fixture, machine, settings, cancellationToken);
}
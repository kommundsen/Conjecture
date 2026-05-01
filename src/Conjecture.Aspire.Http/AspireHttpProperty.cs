// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;
using Conjecture.Core;

using Conjecture.Abstractions.Aspire;
using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.Http;

/// <summary>Convenience entry point for running HTTP-based Aspire stateful property tests.</summary>
public static class AspireHttpProperty
{
    /// <summary>
    /// Runs a stateful property test against an Aspire distributed application using HTTP interactions.
    /// </summary>
    /// <typeparam name="TState">The state type used by the state machine.</typeparam>
    /// <param name="fixture">The fixture that owns the distributed application lifecycle.</param>
    /// <param name="machine">The state machine describing commands and invariants.</param>
    /// <param name="settings">Settings controlling example count and seed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task RunAsync<TState>(
        IAspireAppFixture fixture,
        InteractionStateMachine<TState> machine,
        ConjectureSettings settings,
        CancellationToken cancellationToken)
        => AspireProperty.RunAsync(
            fixture,
            machine,
            static app => new DistributedApplicationHttpTarget(app),
            settings,
            cancellationToken);
}

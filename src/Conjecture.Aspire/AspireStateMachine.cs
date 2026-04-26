// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Net.Http;

using Aspire.Hosting;
using Aspire.Hosting.Testing;

using Conjecture.Core;

namespace Conjecture.Aspire;

/// <summary>
/// Base class for stateful property tests against an Aspire distributed application.
/// </summary>
/// <typeparam name="TState">The type representing the system's state.</typeparam>
public abstract class AspireStateMachine<TState> : IStateMachine<TState, Interaction>
{
    internal DistributedApplication? App { get; set; }

    /// <summary>Returns an <see cref="HttpClient"/> connected to the named resource.</summary>
    protected HttpClient GetClient(string resourceName) =>
        (App ?? throw new InvalidOperationException("App has not been started.")).CreateHttpClient(resourceName);

    /// <inheritdoc />
    public abstract TState InitialState();

    /// <inheritdoc />
    public abstract IEnumerable<Strategy<Interaction>> Commands(TState state);

    /// <inheritdoc />
    public abstract TState RunCommand(TState state, Interaction cmd);

    /// <inheritdoc />
    public abstract void Invariant(TState state);
}
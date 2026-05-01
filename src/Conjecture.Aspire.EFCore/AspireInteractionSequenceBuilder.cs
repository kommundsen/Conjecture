// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Conjecture.Core;
using Conjecture.Http;
using Conjecture.Messaging;

using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.EFCore;

/// <summary>
/// Fluent builder that accumulates interaction-step strategies and produces a
/// <see cref="Strategy{T}"/> that generates interleaved sequences of <see cref="IAddressedInteraction"/>.
/// </summary>
public sealed class AspireInteractionSequenceBuilder
{
    private readonly List<Strategy<IAddressedInteraction>> steps = [];

    /// <summary>
    /// Registers an HTTP step strategy. <paramref name="resourceName"/> overwrites
    /// <see cref="HttpInteraction.ResourceName"/> so the composite target can route the step
    /// to the correct <see cref="System.Net.Http.HttpClient"/> regardless of how the strategy
    /// was originally constructed.
    /// </summary>
    public AspireInteractionSequenceBuilder Http(string resourceName, Strategy<HttpInteraction> step)
    {
        ArgumentNullException.ThrowIfNull(resourceName);
        ArgumentNullException.ThrowIfNull(step);
        steps.Add(step.Select(interaction => (IAddressedInteraction)(interaction with { ResourceName = resourceName })));
        return this;
    }

    /// <summary>
    /// Registers a message step strategy. <paramref name="resourceName"/> is used as the routing
    /// address for <see cref="AddressedMessageInteraction"/>, overriding any destination embedded
    /// in <paramref name="step"/>.
    /// </summary>
    public AspireInteractionSequenceBuilder Message(string resourceName, Strategy<MessageInteraction> step)
    {
        ArgumentNullException.ThrowIfNull(resourceName);
        ArgumentNullException.ThrowIfNull(step);
        steps.Add(step.Select(msg => (IAddressedInteraction)new Conjecture.Messaging.AddressedMessageInteraction(resourceName, msg)));
        return this;
    }

    /// <summary>Registers a deterministic DB snapshot step for <paramref name="resourceName"/>.</summary>
    public AspireInteractionSequenceBuilder DbSnapshot(
        string resourceName,
        string label,
        Func<DbContext, Task<object?>> capture)
    {
        ArgumentNullException.ThrowIfNull(resourceName);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(capture);
        DbSnapshotInteraction snapshot = new(resourceName, label, capture);
        steps.Add(Strategy.Just<IAddressedInteraction>(snapshot));
        return this;
    }

    /// <summary>
    /// Builds a strategy that generates a list of <see cref="IAddressedInteraction"/> of length
    /// in [<paramref name="minSize"/>, <paramref name="maxSize"/>], picking each step uniformly
    /// from all registered step strategies.
    /// </summary>
    public Strategy<IReadOnlyList<IAddressedInteraction>> Build(int minSize = 1, int maxSize = 20)
    {
        if (steps.Count == 0)
        {
            throw new InvalidOperationException("At least one step strategy must be registered before calling Build.");
        }

        Strategy<IAddressedInteraction> stepStrategy = Strategy.OneOf(steps.ToArray());
        return Strategy.Lists(stepStrategy, minSize, maxSize)
            .Select(static list => (IReadOnlyList<IAddressedInteraction>)list);
    }
}
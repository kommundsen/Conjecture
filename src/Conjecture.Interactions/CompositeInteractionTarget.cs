// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Conjecture.Interactions;

/// <summary>Dispatches interactions to named targets based on <see cref="IAddressedInteraction.ResourceName"/>.</summary>
public sealed class CompositeInteractionTarget : IInteractionTarget
{
    private readonly Dictionary<string, IInteractionTarget> targets;

    /// <summary>Initializes a new <see cref="CompositeInteractionTarget"/> with the given named targets.</summary>
    public CompositeInteractionTarget(params (string name, IInteractionTarget target)[] targets)
    {
        this.targets = new(targets.Length);
        foreach ((string name, IInteractionTarget target) in targets)
        {
            this.targets[name] = target;
        }
    }

    /// <inheritdoc/>
    public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
    {
        if (interaction is not IAddressedInteraction addressed)
        {
            throw new InvalidOperationException(
                $"CompositeInteractionTarget requires interactions that implement {nameof(IAddressedInteraction)}, " +
                $"but received {interaction.GetType().Name}.");
        }

        if (!targets.TryGetValue(addressed.ResourceName, out IInteractionTarget? target))
        {
            string registered = string.Join(", ", targets.Keys.Select(static k => $"'{k}'"));
            throw new InvalidOperationException(
                $"No target registered for resource '{addressed.ResourceName}'. Registered names: {registered}.");
        }

        return target.ExecuteAsync(interaction, ct);
    }
}
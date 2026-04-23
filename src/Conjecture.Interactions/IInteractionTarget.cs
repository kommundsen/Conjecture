// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Threading;
using System.Threading.Tasks;

namespace Conjecture.Interactions;

/// <summary>Executes an <see cref="IInteraction"/> and returns an optional result.</summary>
public interface IInteractionTarget
{
    /// <summary>Executes the given <paramref name="interaction"/>.</summary>
    Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct);
}
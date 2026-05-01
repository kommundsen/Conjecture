// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.EFCore;

using Conjecture.Abstractions.Aspire;

namespace Conjecture.Aspire.EFCore;

/// <summary>
/// Holds a collection of <see cref="IDbTarget"/> instances and resets them sequentially between
/// property-test examples.
/// </summary>
/// <remarks>
/// <para>
/// Typical usage inside an <see cref="Conjecture.Abstractions.Aspire.IAspireAppFixture"/> subclass:
/// </para>
/// <code>
/// private readonly AspireDbTargetRegistry _registry;
///
/// public MyFixture(IDbTarget target)
/// {
///     _registry = new AspireDbTargetRegistry();
///     _registry.Register(target);
/// }
///
/// public override Task ResetAsync(DistributedApplication app, CancellationToken ct = default) =>
///     _registry.ResetAllAsync(ct);
///
/// public override async ValueTask DisposeAsync()
/// {
///     await _registry.DisposeAsync().ConfigureAwait(false);
///     await base.DisposeAsync().ConfigureAwait(false);
/// }
/// </code>
/// </remarks>
public sealed class AspireDbTargetRegistry : IAsyncDisposable
{
    private readonly List<IDbTarget> targets = [];

    /// <summary>Gets the registered targets in registration order.</summary>
    public IReadOnlyList<IDbTarget> Targets => targets;

    /// <summary>
    /// Registers <paramref name="target"/> and returns <see langword="this"/> for fluent chaining.
    /// </summary>
    public AspireDbTargetRegistry Register(IDbTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        targets.Add(target);
        return this;
    }

    /// <summary>
    /// Resets all registered targets sequentially. Stops and propagates on the first exception.
    /// </summary>
    public async Task ResetAllAsync(CancellationToken ct = default)
    {
        foreach (IDbTarget target in targets)
        {
            await target.ResetAsync(target.ResourceName, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes any registered targets that implement <see cref="IAsyncDisposable"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (IDbTarget target in targets)
        {
            if (target is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
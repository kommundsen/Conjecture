// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Interactions;

using Microsoft.EntityFrameworkCore;

namespace Conjecture.EFCore;

/// <summary>EF Core InMemory-backed interaction target, keyed by a single resource name.</summary>
public sealed class InMemoryDbTarget(string resourceName, Func<DbContext> contextFactory) : IDbTarget
{
    private readonly Func<DbContext> contextFactory = contextFactory
        ?? throw new ArgumentNullException(nameof(contextFactory));

    /// <summary>Gets the resource name this target is registered under.</summary>
    public string ResourceName { get; } = string.IsNullOrEmpty(resourceName)
        ? throw new ArgumentException("Resource name must not be null or empty.", nameof(resourceName))
        : resourceName;

    /// <inheritdoc/>
    public DbContext ResolveContext(string resourceName) =>
        resourceName == ResourceName
            ? contextFactory()
            : throw new InvalidOperationException(
                $"Unknown resource '{resourceName}'; this target is registered for '{ResourceName}'.");

    /// <inheritdoc/>
    public async Task ResetAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        if (resourceName != ResourceName)
        {
            throw new InvalidOperationException(
                $"Unknown resource '{resourceName}'; this target is registered for '{ResourceName}'.");
        }

        await using DbContext ctx = contextFactory();
        await ctx.Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
        await ctx.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct) =>
        throw new NotImplementedException("Dispatch logic will be implemented in a subsequent cycle.");
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Interactions;

using Microsoft.EntityFrameworkCore;

namespace Conjecture.EFCore;

/// <summary>SQLite-backed interaction target, keyed by a single resource name.</summary>
public sealed class SqliteDbTarget(string resourceName, Func<DbContext> contextFactory) : IDbTarget, IAsyncDisposable
{
    private readonly Func<DbContext> contextFactory = contextFactory
        ?? throw new ArgumentNullException(nameof(contextFactory));

    private bool disposed;

    /// <summary>Gets the resource name this target is registered under.</summary>
    public string ResourceName { get; } = string.IsNullOrEmpty(resourceName)
        ? throw new ArgumentException("Resource name must not be null or empty.", nameof(resourceName))
        : resourceName;

    /// <inheritdoc/>
    public DbContext ResolveContext(string resourceName)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return resourceName == ResourceName
            ? contextFactory()
            : throw new InvalidOperationException(
                $"Unknown resource '{resourceName}'; this target is registered for '{ResourceName}'.");
    }

    /// <inheritdoc/>
    public async Task ResetAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

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
    /// <remarks>
    /// Each <see cref="ExecuteAsync"/> call opens its own <see cref="DbContext"/>. Sequencing an
    /// <see cref="DbOpKind.Add"/> and a separate <see cref="DbOpKind.SaveChanges"/> across two
    /// <c>ExecuteAsync</c> calls will NOT see each other through the change tracker — the second
    /// call sees an empty context. Use <c>Generate.Db.Sequence</c> to produce a single state-machine
    /// run that batches operations correctly.
    /// </remarks>
    public async Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(interaction);

        if (interaction is not DbInteraction db)
        {
            throw new InvalidOperationException(
                $"SqliteDbTarget cannot execute interactions of type '{interaction.GetType().Name}'.");
        }

        await using DbContext ctx = ResolveContext(db.ResourceName);

        return db.Op switch
        {
            DbOpKind.Add => AddEntity(ctx, db.Payload!),
            DbOpKind.Update => UpdateEntity(ctx, db.Payload!),
            DbOpKind.Remove => RemoveEntity(ctx, db.Payload!),
            DbOpKind.SaveChanges => (object)await ctx.SaveChangesAsync(ct).ConfigureAwait(false),
            DbOpKind.Query => throw new NotSupportedException("Query op is not supported in v1."),
            _ => throw new InvalidOperationException($"Unknown DbOpKind '{db.Op}'.")
        };
    }

    private static object? AddEntity(DbContext ctx, object payload)
    {
        ctx.Add(payload);
        return null;
    }

    private static object? UpdateEntity(DbContext ctx, object payload)
    {
        ctx.Update(payload);
        return null;
    }

    private static object? RemoveEntity(DbContext ctx, object payload)
    {
        ctx.Remove(payload);
        return null;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        disposed = true;
        return ValueTask.CompletedTask;
    }
}
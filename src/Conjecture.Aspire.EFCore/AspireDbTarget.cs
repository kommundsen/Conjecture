// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;
using Aspire.Hosting.Testing;

using Conjecture.EFCore;
using Conjecture.Interactions;

using Microsoft.EntityFrameworkCore;

namespace Conjecture.Aspire.EFCore;

/// <summary>
/// EF Core interaction target that resolves <typeparamref name="TContext"/> via a user-supplied
/// factory seeded with the connection string obtained from an Aspire <see cref="DistributedApplication"/>.
/// </summary>
/// <typeparam name="TContext">The <see cref="DbContext"/> type to construct per call.</typeparam>
public sealed class AspireDbTarget<TContext> : IDbTarget, IAsyncDisposable
    where TContext : DbContext
{
    private readonly Func<string, TContext> contextFactory;
    private readonly string connectionString;
    private readonly List<TContext> trackedContexts = [];
    private readonly object contextsLock = new();

    private AspireDbTarget(string resourceName, string connectionString, Func<string, TContext> contextFactory)
    {
        ResourceName = resourceName;
        this.connectionString = connectionString;
        this.contextFactory = contextFactory;
    }

    /// <inheritdoc/>
    public string ResourceName { get; }

    /// <summary>
    /// Creates a new <see cref="AspireDbTarget{TContext}"/> by resolving the connection string via
    /// <paramref name="connectionStringResolver"/> for <paramref name="resourceName"/> and passing it to
    /// <paramref name="contextFactory"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the resolver returns <see langword="null"/> for the given resource name.
    /// </exception>
    public static async Task<AspireDbTarget<TContext>> CreateAsync(
        ConnectionStringResolver connectionStringResolver,
        string resourceName,
        Func<string, TContext> contextFactory,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connectionStringResolver);
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentNullException.ThrowIfNull(contextFactory);

        string? connStr = await connectionStringResolver(resourceName, ct).ConfigureAwait(false);
        return new(resourceName, connStr ?? throw new InvalidOperationException(
            FormattableString.Invariant($"No connection string found for resource '{resourceName}'.")), contextFactory);
    }

    /// <summary>
    /// Creates a new <see cref="AspireDbTarget{TContext}"/> using <paramref name="connectionStringResolver"/>
    /// with a default <see cref="CancellationToken"/>.
    /// </summary>
    public static Task<AspireDbTarget<TContext>> CreateAsync(
        ConnectionStringResolver connectionStringResolver,
        string resourceName,
        Func<string, TContext> contextFactory) =>
        CreateAsync(connectionStringResolver, resourceName, contextFactory, default);

    /// <summary>
    /// Creates a new <see cref="AspireDbTarget{TContext}"/> by resolving the connection string for
    /// <paramref name="resourceName"/> from <paramref name="app"/> and passing it to
    /// <paramref name="contextFactory"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the resource is not registered or has no connection string.
    /// </exception>
    public static Task<AspireDbTarget<TContext>> CreateAsync(
        DistributedApplication app,
        string resourceName,
        Func<string, TContext> contextFactory,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(app);
        return CreateAsync(
            async (name, token) => await app.GetConnectionStringAsync(name, token).ConfigureAwait(false),
            resourceName,
            contextFactory,
            ct);
    }

    /// <summary>
    /// Creates a new <see cref="AspireDbTarget{TContext}"/> by resolving the connection string for
    /// <paramref name="resourceName"/> from <paramref name="app"/> with a default cancellation token.
    /// </summary>
    public static Task<AspireDbTarget<TContext>> CreateAsync(
        DistributedApplication app,
        string resourceName,
        Func<string, TContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(app);
        return CreateAsync(app, resourceName, contextFactory, default);
    }

    /// <inheritdoc/>
    public DbContext ResolveContext(string resourceName)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        if (!string.Equals(resourceName, ResourceName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                FormattableString.Invariant($"AspireDbTarget<{typeof(TContext).Name}> registered as '{ResourceName}' cannot resolve '{resourceName}'."));
        }

        TContext ctx = contextFactory(connectionString);
        lock (contextsLock)
        {
            trackedContexts.Add(ctx);
        }

        return ctx;
    }

    /// <summary>Creates a fresh <typeparamref name="TContext"/> via the captured factory and tracks it for disposal.</summary>
    public TContext Resolve()
    {
        return (TContext)ResolveContext(ResourceName);
    }

    /// <inheritdoc/>
    public async Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (interaction is DbSnapshotInteraction snapshot)
        {
            if (!string.Equals(snapshot.ResourceName, ResourceName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"AspireDbTarget<{typeof(TContext).Name}> registered as '{ResourceName}' cannot handle snapshot for '{snapshot.ResourceName}'."));
            }

            TContext snapshotCtx = contextFactory(connectionString);
            await using (snapshotCtx)
            {
                return await snapshot.Capture(snapshotCtx).ConfigureAwait(false);
            }
        }

        if (interaction is not DbInteraction db)
        {
            throw new InvalidOperationException(
                FormattableString.Invariant($"AspireDbTarget<{typeof(TContext).Name}> cannot execute interactions of type '{interaction.GetType().Name}'."));
        }

        TContext ctx = contextFactory(connectionString);
        await using (ctx)
        {
            return db.Op switch
            {
                DbOpKind.Add => Stage(ctx, db.Payload, EntityState.Added),
                DbOpKind.Update => Stage(ctx, db.Payload, EntityState.Modified),
                DbOpKind.Remove => Stage(ctx, db.Payload, EntityState.Deleted),
                DbOpKind.SaveChanges => (object?)await ctx.SaveChangesAsync(ct).ConfigureAwait(false),
                DbOpKind.Query => throw new NotSupportedException("Query op is not supported in v1."),
                _ => throw new InvalidOperationException(FormattableString.Invariant($"Unknown DbOpKind '{db.Op}'."))
            };
        }

        static object? Stage(DbContext ctx, object? payload, EntityState state)
        {
            if (payload is not null)
            {
                ctx.Entry(payload).State = state;
            }

            return null;
        }
    }

    /// <inheritdoc/>
    public async Task ResetAsync(string resourceName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        if (!string.Equals(resourceName, ResourceName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                FormattableString.Invariant($"AspireDbTarget<{typeof(TContext).Name}> registered as '{ResourceName}' cannot reset '{resourceName}'."));
        }

        TContext ctx = contextFactory(connectionString);
        await using (ctx)
        {
            await ctx.Database.EnsureDeletedAsync(ct).ConfigureAwait(false);
            await ctx.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>Disposes all tracked contexts created via <see cref="ResolveContext"/>.</summary>
    public async ValueTask DisposeAsync()
    {
        List<TContext> toDispose;
        lock (contextsLock)
        {
            toDispose = [.. trackedContexts];
            trackedContexts.Clear();
        }

        foreach (TContext ctx in toDispose)
        {
            await ctx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
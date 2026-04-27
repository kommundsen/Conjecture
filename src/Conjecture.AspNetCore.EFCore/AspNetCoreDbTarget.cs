// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.EFCore;
using Conjecture.Interactions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conjecture.AspNetCore.EFCore;

/// <summary>
/// EF Core interaction target that resolves <typeparamref name="TContext"/> from an <see cref="IHost"/>'s
/// DI container via a fresh <see cref="IServiceScope"/> per call.
/// </summary>
/// <typeparam name="TContext">The <see cref="DbContext"/> type registered in the host's DI container.</typeparam>
public sealed class AspNetCoreDbTarget<TContext> : IDbTarget, IAsyncDisposable
    where TContext : DbContext
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly List<IServiceScope> openScopes = [];
    private readonly object scopesLock = new();

    /// <summary>Initializes a new instance of <see cref="AspNetCoreDbTarget{TContext}"/>.</summary>
    /// <param name="host">The application host whose DI container provides <typeparamref name="TContext"/>.</param>
    /// <param name="resourceName">Logical resource name this target is registered under.</param>
    public AspNetCoreDbTarget(IHost host, string resourceName)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        scopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();
        ResourceName = resourceName;
    }

    /// <inheritdoc/>
    public string ResourceName { get; }

    /// <inheritdoc/>
    public DbContext ResolveContext(string resourceName)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        if (!string.Equals(resourceName, ResourceName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                FormattableString.Invariant($"AspNetCoreDbTarget<{typeof(TContext).Name}> registered as '{ResourceName}' cannot resolve '{resourceName}'."));
        }

        IServiceScope scope = scopeFactory.CreateScope();
        lock (scopesLock)
        {
            openScopes.Add(scope);
        }

        return scope.ServiceProvider.GetRequiredService<TContext>();
    }

    /// <summary>Creates a fresh DI scope and returns the typed <typeparamref name="TContext"/> from it.</summary>
    public TContext Resolve()
    {
        return (TContext)ResolveContext(ResourceName);
    }

    /// <inheritdoc/>
    public async Task ResetAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        if (!string.Equals(resourceName, ResourceName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                FormattableString.Invariant($"AspNetCoreDbTarget<{typeof(TContext).Name}> registered as '{ResourceName}' cannot reset '{resourceName}'."));
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        TContext ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        await ctx.Database.EnsureDeletedAsync(cancellationToken).ConfigureAwait(false);
        await ctx.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (interaction is not DbInteraction db)
        {
            throw new InvalidOperationException(
                FormattableString.Invariant($"AspNetCoreDbTarget<{typeof(TContext).Name}> cannot execute interactions of type '{interaction.GetType().Name}'."));
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        DbContext ctx = scope.ServiceProvider.GetRequiredService<TContext>();

        return db.Op switch
        {
            DbOpKind.Add => Stage(ctx, db.Payload, EntityState.Added),
            DbOpKind.Update => Stage(ctx, db.Payload, EntityState.Modified),
            DbOpKind.Remove => Stage(ctx, db.Payload, EntityState.Deleted),
            DbOpKind.SaveChanges => (object?)await ctx.SaveChangesAsync(ct).ConfigureAwait(false),
            DbOpKind.Query => throw new NotSupportedException("Query op is not supported in v1."),
            _ => throw new InvalidOperationException(FormattableString.Invariant($"Unknown DbOpKind '{db.Op}'."))
        };

        static object? Stage(DbContext ctx, object? payload, EntityState state)
        {
            if (payload is not null)
            {
                ctx.Entry(payload).State = state;
            }

            return null;
        }
    }

    /// <summary>Disposes all open DI scopes tracked by this target.</summary>
    public async ValueTask DisposeAsync()
    {
        List<IServiceScope> toDispose;
        lock (scopesLock)
        {
            toDispose = [.. openScopes];
            openScopes.Clear();
        }

        foreach (IServiceScope s in toDispose)
        {
            if (s is IAsyncDisposable a)
            {
                await a.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                s.Dispose();
            }
        }
    }
}
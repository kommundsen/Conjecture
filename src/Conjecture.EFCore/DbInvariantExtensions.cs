// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Conjecture.EFCore;

/// <summary>Extension methods that assert common DB invariants against an <see cref="IDbTarget"/>.</summary>
public static class DbInvariantExtensions
{
    /// <summary>
    /// Saves <paramref name="entity"/> via <paramref name="target"/>, reloads it in a fresh context,
    /// and asserts equality using <paramref name="comparer"/> (or a default scalar-property comparer).
    /// </summary>
    public static Task AssertRoundtripAsync<TEntity>(
        this IDbTarget target,
        TEntity entity,
        IEqualityComparer<TEntity>? comparer = null,
        CancellationToken ct = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(entity);

        string resourceName = target.ResourceName;
        return RoundtripAsserter.AssertRoundtripAsync(
            () => target.ResolveContext(resourceName),
            entity,
            comparer,
            ct);
    }

    /// <summary>
    /// Saves <paramref name="entity"/>, opens two overlapping contexts, applies <paramref name="mutateFirst"/>
    /// in the first context and saves (advancing the concurrency token), then applies <paramref name="mutateSecond"/>
    /// in the second context (which holds the stale token) and asserts that the save throws
    /// <see cref="DbUpdateConcurrencyException"/>.
    /// </summary>
    public static async Task AssertConcurrencyTokenRespectedAsync<TEntity>(
        this IDbTarget target,
        TEntity entity,
        Action<TEntity> mutateFirst,
        Action<TEntity> mutateSecond,
        CancellationToken ct = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(mutateFirst);
        ArgumentNullException.ThrowIfNull(mutateSecond);

        string resourceName = target.ResourceName;
        object?[] keyValues;

        // Seed the entity so it exists in the DB with a concurrency token.
        await using (DbContext seedCtx = target.ResolveContext(resourceName))
        {
            seedCtx.Add(entity);
            await seedCtx.SaveChangesAsync(ct).ConfigureAwait(false);
            keyValues = GetKeyValues(seedCtx, entity);
        }

        // Open ctxB (stale): loads the entity before ctxA makes any changes.
        await using DbContext ctxB = target.ResolveContext(resourceName);
        TEntity? staleEntity = await ctxB.Set<TEntity>().FindAsync(keyValues, ct).ConfigureAwait(false);
        if (staleEntity is null)
        {
            throw new RoundtripAssertionException(
                FormattableString.Invariant($"Entity of type '{typeof(TEntity).Name}' could not be reloaded for concurrency test."));
        }

        // ctxA: apply first mutation and save — advances the concurrency token in the DB.
        await using (DbContext ctxA = target.ResolveContext(resourceName))
        {
            TEntity? firstEntity = await ctxA.Set<TEntity>().FindAsync(keyValues, ct).ConfigureAwait(false);
            if (firstEntity is null)
            {
                throw new RoundtripAssertionException(
                    FormattableString.Invariant($"Entity of type '{typeof(TEntity).Name}' could not be reloaded for first mutation."));
            }

            mutateFirst(firstEntity);
            await ctxA.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        // ctxB: apply second mutation — ctxB still tracks the stale token, so save should fail.
        // The mutator delegate is responsible for bumping the concurrency token on the entity
        // (e.g. `e.Version++` for an `[ConcurrencyCheck] int Version`); EF compares the in-memory
        // token against the persisted one and raises DbUpdateConcurrencyException on mismatch.
        mutateSecond(staleEntity);
        bool concurrencyExceptionThrown = false;
        try
        {
            await ctxB.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            concurrencyExceptionThrown = true;
        }

        if (!concurrencyExceptionThrown)
        {
            throw new RoundtripAssertionException(
                "Expected concurrency-token check to fail on stale update; second SaveChanges succeeded.");
        }
    }

    /// <summary>
    /// Checks every required foreign key in the model and throws if any child row references a non-existent parent.
    /// Uses EF LINQ queries — no raw SQL.
    /// </summary>
    public static async Task AssertNoOrphansAsync(
        this IDbTarget target,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        string resourceName = target.ResourceName;
        await using DbContext ctx = target.ResolveContext(resourceName);
        IModel model = ctx.Model;

        foreach (IEntityType childType in model.GetEntityTypes())
        {
            foreach (IForeignKey fk in childType.GetForeignKeys().Where(static f => f.IsRequired))
            {
                IEntityType parentType = fk.PrincipalEntityType;
                Type childClrType = childType.ClrType;
                Type parentClrType = parentType.ClrType;

                IReadOnlyList<IProperty> fkProps = fk.Properties;
                IReadOnlyList<IProperty> principalKeyProps = fk.PrincipalKey.Properties;

                // Walk every child row and verify its parent exists.
                System.Collections.IEnumerable children = (System.Collections.IEnumerable)ctx
                    .GetType()
                    .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                    .MakeGenericMethod(childClrType)
                    .Invoke(ctx, null)!;

                foreach (object child in children)
                {
                    object?[] fkValues = fkProps
                        .Select(p => p.PropertyInfo!.GetValue(child))
                        .ToArray();

                    object? parent = await ctx.FindAsync(parentClrType, fkValues, ct).ConfigureAwait(false);

                    if (parent is null)
                    {
                        string fkColumns = string.Join(
                            ", ",
                            fkProps.Select(static p => p.Name));

                        throw new RoundtripAssertionException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Found orphan in '{0}' with no matching parent in '{1}' (FK columns: {2}).",
                                childClrType.Name,
                                parentClrType.Name,
                                fkColumns));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Saves <paramref name="entity"/>, then compares the tracked read against an AsNoTracking read,
    /// asserting they are equal using <paramref name="comparer"/> (or a default scalar-property comparer).
    /// </summary>
    public static Task AssertNoTrackingMatchesTrackedAsync<TEntity>(
        this IDbTarget target,
        TEntity entity,
        IEqualityComparer<TEntity>? comparer = null,
        CancellationToken ct = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(entity);

        string resourceName = target.ResourceName;
        return RoundtripAsserter.AssertNoTrackingMatchesTrackedAsync(
            () => target.ResolveContext(resourceName),
            entity,
            comparer,
            ct);
    }

    private static object?[] GetKeyValues<TEntity>(DbContext context, TEntity entity) where TEntity : class
    {
        IEntityType entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(FormattableString.Invariant($"Entity type '{typeof(TEntity).Name}' not found in model."));

        IKey primaryKey = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException(FormattableString.Invariant($"Entity type '{typeof(TEntity).Name}' has no primary key."));

        return [.. primaryKey.Properties.Select(p => p.PropertyInfo!.GetValue(entity))];
    }
}
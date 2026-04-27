// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.EFCore;

/// <summary>Asserts that entities roundtrip through EF Core without data loss.</summary>
public static class RoundtripAsserter
{
    /// <summary>
    /// Saves <paramref name="entity"/> via <paramref name="factory"/>, reloads it in a fresh context,
    /// and asserts equality using <paramref name="comparer"/> (or a default scalar-property comparer).
    /// </summary>
    public static async Task AssertRoundtripAsync<TEntity>(
        Func<DbContext> factory,
        TEntity entity,
        IEqualityComparer<TEntity>? comparer = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(entity);

        object?[] keyValues;

        await using DbContext saveContext = factory();
        saveContext.Add(entity);
        await saveContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        keyValues = GetKeyValues(saveContext, entity);
        await saveContext.DisposeAsync().ConfigureAwait(false);

        await using DbContext readContext = factory();
        TEntity? reloaded = await readContext.Set<TEntity>().FindAsync(keyValues, cancellationToken).ConfigureAwait(false);

        if (reloaded is null)
        {
            throw new RoundtripAssertionException(
                FormattableString.Invariant($"Entity of type '{typeof(TEntity).Name}' could not be reloaded after save."));
        }

        AssertEqual(readContext.Model, entity, reloaded, comparer, "roundtrip reload");
    }

    /// <summary>
    /// Saves <paramref name="entity"/>, then compares the tracked read against an AsNoTracking read,
    /// asserting they are equal using <paramref name="comparer"/> (or a default scalar-property comparer).
    /// </summary>
    public static async Task AssertNoTrackingMatchesTrackedAsync<TEntity>(
        Func<DbContext> factory,
        TEntity entity,
        IEqualityComparer<TEntity>? comparer = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(entity);

        object?[] keyValues;

        await using DbContext saveContext = factory();
        saveContext.Add(entity);
        await saveContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        keyValues = GetKeyValues(saveContext, entity);
        await saveContext.DisposeAsync().ConfigureAwait(false);

        await using DbContext trackedContext = factory();
        TEntity? tracked = await trackedContext.Set<TEntity>().FindAsync(keyValues, cancellationToken).ConfigureAwait(false);

        if (tracked is null)
        {
            throw new RoundtripAssertionException(
                FormattableString.Invariant($"Entity of type '{typeof(TEntity).Name}' could not be reloaded (tracked) after save."));
        }

        await trackedContext.DisposeAsync().ConfigureAwait(false);

        await using DbContext noTrackingContext = factory();
        IEntityType entityType = noTrackingContext.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(FormattableString.Invariant($"Entity type '{typeof(TEntity).Name}' not found in model."));

        IKey primaryKey = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException(FormattableString.Invariant($"Entity type '{typeof(TEntity).Name}' has no primary key."));

        IQueryable<TEntity> query = noTrackingContext.Set<TEntity>().AsNoTracking();
        foreach ((IProperty keyProp, object? keyVal) in primaryKey.Properties.Zip(keyValues))
        {
            object? capturedVal = keyVal;
            string propName = keyProp.Name;
            query = query.Where(e => EF.Property<object>(e, propName) == capturedVal);
        }

        TEntity? noTracking = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (noTracking is null)
        {
            throw new RoundtripAssertionException(
                FormattableString.Invariant($"Entity of type '{typeof(TEntity).Name}' could not be reloaded (no-tracking) after save."));
        }

        AssertEqual(noTrackingContext.Model, tracked, noTracking, comparer, "no-tracking vs tracked");
    }

    private static object?[] GetKeyValues<TEntity>(DbContext context, TEntity entity) where TEntity : class
    {
        IEntityType entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(FormattableString.Invariant($"Entity type '{typeof(TEntity).Name}' not found in model."));

        IKey primaryKey = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException(FormattableString.Invariant($"Entity type '{typeof(TEntity).Name}' has no primary key."));

        return [.. primaryKey.Properties.Select(p => p.PropertyInfo!.GetValue(entity))];
    }

    private static void AssertEqual<TEntity>(
        IModel model,
        TEntity expected,
        TEntity actual,
        IEqualityComparer<TEntity>? comparer,
        string phase)
        where TEntity : class
    {
        if (comparer is not null)
        {
            if (!comparer.Equals(expected, actual))
            {
                string diffDetail = BuildScalarDiff(model, expected, actual);
                throw new RoundtripAssertionException(
                    FormattableString.Invariant($"Roundtrip assertion failed ({phase}): comparer reported inequality.{diffDetail}"));
            }

            return;
        }

        string defaultDiff = BuildScalarDiff(model, expected, actual);
        if (defaultDiff.Length > 0)
        {
            throw new RoundtripAssertionException(
                FormattableString.Invariant($"Roundtrip assertion failed ({phase}):{defaultDiff}"));
        }
    }

    private static string BuildScalarDiff<TEntity>(IModel model, TEntity expected, TEntity actual)
        where TEntity : class
    {
        IEntityType? entityType = model.FindEntityType(typeof(TEntity));
        if (entityType is null)
        {
            return string.Empty;
        }

        StringBuilder sb = new();

        foreach (IProperty prop in entityType.GetProperties())
        {
            System.Reflection.PropertyInfo? propInfo = prop.PropertyInfo;
            if (propInfo is null)
            {
                continue;
            }

            object? expectedVal = propInfo.GetValue(expected);
            object? actualVal = propInfo.GetValue(actual);

            if (!Equals(expectedVal, actualVal))
            {
                sb.Append(CultureInfo.InvariantCulture, $" Property '{prop.Name}': expected '{expectedVal}', got '{actualVal}'.");
            }
        }

        return sb.ToString();
    }
}
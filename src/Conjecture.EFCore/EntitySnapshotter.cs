// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.EFCore;

/// <summary>Captures and diffs <see cref="EntitySnapshot"/> instances from an <see cref="IDbTarget"/>.</summary>
public static class EntitySnapshotter
{
    /// <summary>Captures a snapshot of all entity types and their primary keys from the target.</summary>
    public static async Task<EntitySnapshot> CaptureAsync(
        IDbTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        await using DbContext ctx = target.ResolveContext(target.ResourceName);
        IModel model = ctx.Model;
        Dictionary<Type, int> counts = [];
        Dictionary<Type, IReadOnlySet<object>> keys = [];

        foreach (IEntityType entityType in model.GetEntityTypes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            Type clr = entityType.ClrType;
            IKey? pk = entityType.FindPrimaryKey();
            if (pk is null)
            {
                continue;
            }

            MethodInfo setMethod = typeof(DbContext)
                .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(clr);
            object? dbSet = setMethod.Invoke(ctx, null);

            HashSet<object> keySet = [];
            int count = 0;

            foreach (object entity in (IEnumerable)dbSet!)
            {
                count++;
                object?[] keyValues = pk.Properties
                    .Select(p => p.PropertyInfo!.GetValue(entity))
                    .ToArray();
                object keyObj = keyValues.Length == 1
                    ? keyValues[0]!
                    : (object)string.Join("|", keyValues.Select(static v => v?.ToString() ?? "null"));
                keySet.Add(keyObj);
            }

            counts[clr] = count;
            keys[clr] = keySet;
        }

        return new EntitySnapshot(counts, keys);
    }

    /// <summary>Computes the diff between two snapshots.</summary>
    public static EntitySnapshotDiff Diff(EntitySnapshot before, EntitySnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        Dictionary<Type, int> countDeltas = [];
        Dictionary<Type, IReadOnlyList<object>> addedKeys = [];
        Dictionary<Type, IReadOnlyList<object>> removedKeys = [];

        HashSet<Type> allTypes = [.. before.Counts.Keys.Concat(after.Counts.Keys).Distinct()];

        foreach (Type t in allTypes)
        {
            int b = before.Counts.GetValueOrDefault(t);
            int a = after.Counts.GetValueOrDefault(t);
            countDeltas[t] = a - b;

            IReadOnlySet<object> bk = before.Keys.GetValueOrDefault(t) ?? new HashSet<object>();
            IReadOnlySet<object> ak = after.Keys.GetValueOrDefault(t) ?? new HashSet<object>();

            IReadOnlyList<object> added = ak.Except(bk).ToList();
            IReadOnlyList<object> removed = bk.Except(ak).ToList();

            if (added.Count > 0)
            {
                addedKeys[t] = added;
            }

            if (removed.Count > 0)
            {
                removedKeys[t] = removed;
            }
        }

        return new EntitySnapshotDiff(countDeltas, addedKeys, removedKeys);
    }
}
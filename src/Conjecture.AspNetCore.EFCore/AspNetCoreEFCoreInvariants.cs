// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.EFCore;
using Conjecture.Http;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Conjecture.AspNetCore.EFCore;

/// <summary>Combines an <see cref="IHttpTarget"/> and an <see cref="IDbTarget"/> to assert HTTP/EF Core invariants.</summary>
public sealed class AspNetCoreEFCoreInvariants(IHttpTarget http, IDbTarget db)
{
    private readonly IHttpTarget http = http ?? throw new ArgumentNullException(nameof(http));
    private readonly IDbTarget db = db ?? throw new ArgumentNullException(nameof(db));

    /// <summary>
    /// Asserts that a failing HTTP request (status &gt;= 400) did not persist any entity changes.
    /// </summary>
    public async Task AssertNoPartialWritesOnErrorAsync(
        Func<HttpClient, CancellationToken, Task<HttpResponseMessage>> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        EntitySnapshot before = await EntitySnapshotter.CaptureAsync(db, cancellationToken).ConfigureAwait(false);

        HttpClient client = http.ResolveClient(db.ResourceName);
        HttpResponseMessage response = await request(client, cancellationToken).ConfigureAwait(false);

        EntitySnapshot after = await EntitySnapshotter.CaptureAsync(db, cancellationToken).ConfigureAwait(false);

        if ((int)response.StatusCode < 400)
        {
            return;
        }

        EntitySnapshotDiff diff = EntitySnapshotter.Diff(before, after);
        if (diff.IsEmpty)
        {
            return;
        }

        string method = response.RequestMessage?.Method.Method ?? "<unknown>";
        string path = response.RequestMessage?.RequestUri?.PathAndQuery ?? "<unknown>";
        int status = (int)response.StatusCode;
        throw new AspNetCoreEFCoreInvariantException(
            FormattableString.Invariant(
                $"AssertNoPartialWritesOnError: {method} {path} returned {status} but persisted changes: {diff.ToReport()}"));
    }

    /// <summary>
    /// Asserts that deleting a root aggregate produces a DB state consistent with the EF model's
    /// <see cref="DeleteBehavior"/> configuration for all dependent foreign keys.
    /// </summary>
    public async Task AssertCascadeCorrectnessAsync(
        Func<HttpClient, CancellationToken, Task<HttpResponseMessage>> deleteRequest,
        Type rootEntityType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deleteRequest);
        ArgumentNullException.ThrowIfNull(rootEntityType);

        // Walk model metadata first (dispose context immediately after).
        List<(IEntityType childType, IReadOnlyList<IProperty> fkProps, IReadOnlyList<IProperty> pkProps, DeleteBehavior expected)> foreignKeys;
        await using (DbContext modelCtx = db.ResolveContext(db.ResourceName))
        {
            foreignKeys = modelCtx.Model.GetEntityTypes()
                .SelectMany(et => et.GetForeignKeys()
                    .Where(fk => fk.PrincipalEntityType.ClrType == rootEntityType)
                    .Select(fk => (
                        childType: et,
                        fkProps: (IReadOnlyList<IProperty>)fk.Properties.ToList(),
                        pkProps: (IReadOnlyList<IProperty>)(et.FindPrimaryKey()?.Properties.ToList() ?? []),
                        expected: fk.DeleteBehavior)))
                .ToList();
        }

        // Capture which root PKs exist and which dependents reference them BEFORE the delete.
        IReadOnlySet<object> rootKeysBefore;
        // Maps FK entry: childPk → fkKey (before delete)
        Dictionary<(Type childType, string fkName), Dictionary<object, object?>> dependentFksBefore = [];

        await using (DbContext snapshotCtx = db.ResolveContext(db.ResourceName))
        {
            // Capture root PKs
            IEntityType? rootEntityMeta = snapshotCtx.Model.GetEntityTypes()
                .FirstOrDefault(et => et.ClrType == rootEntityType);
            IKey? rootPk = rootEntityMeta?.FindPrimaryKey();
            if (rootPk is null)
            {
                return;
            }

            MethodInfo rootSetMethod = typeof(DbContext)
                .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(rootEntityType);
            IEnumerable rootRows = (IEnumerable)rootSetMethod.Invoke(snapshotCtx, null)!;
            HashSet<object> rootKeys = [];
            foreach (object root in rootRows)
            {
                object?[] pkVals = rootPk.Properties.Select(p => p.PropertyInfo!.GetValue(root)).ToArray();
                object pk = pkVals.Length == 1 ? pkVals[0]! : (object)string.Join("|", pkVals.Select(static v => v?.ToString() ?? "null"));
                rootKeys.Add(pk);
            }

            rootKeysBefore = rootKeys;

            // Capture FK values for all dependents
            foreach ((IEntityType childType, IReadOnlyList<IProperty> fkProps, IReadOnlyList<IProperty> pkProps, _) in foreignKeys)
            {
                Type childClr = childType.ClrType;
                string fkName = string.Join(",", fkProps.Select(static p => p.Name));
                MethodInfo childSetMethod = typeof(DbContext)
                    .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                    .MakeGenericMethod(childClr);
                IEnumerable childRows = (IEnumerable)childSetMethod.Invoke(snapshotCtx, null)!;

                Dictionary<object, object?> childPkToFkKey = [];
                foreach (object child in childRows)
                {
                    if (pkProps.Count == 0)
                    {
                        continue;
                    }

                    object?[] childPkVals = pkProps.Select(p => p.PropertyInfo!.GetValue(child)).ToArray();
                    object childPk = childPkVals.Length == 1 ? childPkVals[0]! : (object)string.Join("|", childPkVals.Select(static v => v?.ToString() ?? "null"));

                    object?[] fkVals = fkProps.Select(p => p.PropertyInfo!.GetValue(child)).ToArray();
                    object? fkKey = fkVals.Length == 1
                        ? fkVals[0]
                        : (object)string.Join("|", fkVals.Select(static v => v?.ToString() ?? "null"));

                    childPkToFkKey[childPk] = fkKey;
                }

                dependentFksBefore[(childClr, fkName)] = childPkToFkKey;
            }
        }

        HttpClient client = http.ResolveClient(db.ResourceName);
        HttpResponseMessage response = await deleteRequest(client, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode >= 400)
        {
            return;
        }

        // Determine which root PKs were actually deleted.
        IReadOnlySet<object> rootKeysAfter;
        await using (DbContext afterCtx = db.ResolveContext(db.ResourceName))
        {
            IEntityType? rootEntityMeta = afterCtx.Model.GetEntityTypes()
                .FirstOrDefault(et => et.ClrType == rootEntityType);
            IKey? rootPk = rootEntityMeta?.FindPrimaryKey();
            if (rootPk is null)
            {
                return;
            }

            MethodInfo rootSetMethod = typeof(DbContext)
                .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(rootEntityType);
            IEnumerable rootRows = (IEnumerable)rootSetMethod.Invoke(afterCtx, null)!;
            HashSet<object> remaining = [];
            foreach (object root in rootRows)
            {
                object?[] pkVals = rootPk.Properties.Select(p => p.PropertyInfo!.GetValue(root)).ToArray();
                object pk = pkVals.Length == 1 ? pkVals[0]! : (object)string.Join("|", pkVals.Select(static v => v?.ToString() ?? "null"));
                remaining.Add(pk);
            }

            rootKeysAfter = remaining;
        }

        IReadOnlyList<object> removedRootKeys = rootKeysBefore.Except(rootKeysAfter).ToList();
        if (removedRootKeys.Count == 0)
        {
            return;
        }

        // Verify each FK relationship against the expected behavior.
        foreach ((IEntityType childType, IReadOnlyList<IProperty> fkProps, IReadOnlyList<IProperty> pkProps, DeleteBehavior expected) in foreignKeys)
        {
            Type childClr = childType.ClrType;
            string fkName = string.Join(",", fkProps.Select(static p => p.Name));

            if (!dependentFksBefore.TryGetValue((childClr, fkName), out Dictionary<object, object?>? beforeFkMap))
            {
                continue;
            }

            // Find dependents that BEFORE the delete referenced a deleted root.
            List<(object childPk, object? fkKey)> affectedBefore = beforeFkMap
                .Where(kv => kv.Value is not null && removedRootKeys.Any(rk => Equals(rk, kv.Value)))
                .Select(static kv => (kv.Key, kv.Value))
                .ToList();

            if (affectedBefore.Count == 0)
            {
                continue;
            }

            // Query current state of affected dependents.
            await using DbContext verifyCtx = db.ResolveContext(db.ResourceName);
            MethodInfo setMethod = typeof(DbContext)
                .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(childClr);
            IEnumerable childRowsNow = (IEnumerable)setMethod.Invoke(verifyCtx, null)!;

            Dictionary<object, object?> currentFkMap = [];
            foreach (object child in childRowsNow)
            {
                if (pkProps.Count == 0)
                {
                    continue;
                }

                object?[] childPkVals = pkProps.Select(p => p.PropertyInfo!.GetValue(child)).ToArray();
                object childPk = childPkVals.Length == 1 ? childPkVals[0]! : (object)string.Join("|", childPkVals.Select(static v => v?.ToString() ?? "null"));

                object?[] fkVals = fkProps.Select(p => p.PropertyInfo!.GetValue(child)).ToArray();
                object? fkKey = fkVals.Length == 1
                    ? fkVals[0]
                    : (object)string.Join("|", fkVals.Select(static v => v?.ToString() ?? "null"));

                currentFkMap[childPk] = fkKey;
            }

            foreach ((object childPk, object? oldFkKey) in affectedBefore)
            {
                bool existsAfter = currentFkMap.TryGetValue(childPk, out object? newFkKey);

                switch (expected)
                {
                    case DeleteBehavior.Cascade:
                    case DeleteBehavior.ClientCascade:
                        if (existsAfter)
                        {
                            throw new AspNetCoreEFCoreInvariantException(
                                FormattableString.Invariant(
                                    $"AssertCascadeCorrectness: FK {childClr.Name}.{fkName} → {rootEntityType.Name} expected '{expected}' but dependent row (pk='{childPk}') still exists after parent key '{oldFkKey}' was deleted."));
                        }

                        break;
                    case DeleteBehavior.SetNull:
                    case DeleteBehavior.ClientSetNull:
                        if (existsAfter && newFkKey is not null)
                        {
                            throw new AspNetCoreEFCoreInvariantException(
                                FormattableString.Invariant(
                                    $"AssertCascadeCorrectness: FK {childClr.Name}.{fkName} → {rootEntityType.Name} expected 'SetNull' but FK column still references deleted parent key '{oldFkKey}' on row pk='{childPk}'."));
                        }

                        break;
                    case DeleteBehavior.Restrict:
                    case DeleteBehavior.NoAction:
                        // If we reach here the root was deleted; that is the bug.
                        throw new AspNetCoreEFCoreInvariantException(
                            FormattableString.Invariant(
                                $"AssertCascadeCorrectness: FK {childClr.Name}.{fkName} → {rootEntityType.Name} expected '{expected}' but root delete succeeded with surviving dependent (pk='{childPk}') referencing key '{oldFkKey}'."));
                }
            }
        }
    }
}
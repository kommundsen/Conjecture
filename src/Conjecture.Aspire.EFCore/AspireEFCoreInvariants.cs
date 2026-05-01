// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.EFCore;
using Conjecture.Interactions;

using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.EFCore;

/// <summary>
/// Combines an <see cref="IInteractionTarget"/> writer and an <see cref="IDbTarget"/> to assert
/// Aspire EF Core invariants across any interaction shape (HTTP, Messaging, gRPC, etc.).
/// </summary>
public sealed class AspireEFCoreInvariants(IInteractionTarget writer, IDbTarget db)
{
    private readonly IInteractionTarget writer = writer ?? throw new ArgumentNullException(nameof(writer));
    private readonly IDbTarget db = db ?? throw new ArgumentNullException(nameof(db));

    /// <summary>
    /// Snapshots the row count, executes the interaction, then asserts:
    /// if it threw — row count is unchanged (no partial write);
    /// if it succeeded — row count grew by at least one.
    /// </summary>
    public async Task AssertNoPartialWritesOnErrorAsync(
        IInteraction operation,
        Func<DbContext, Task<int>> rowCount,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(rowCount);

        int before = await ReadCountAsync(rowCount, ct).ConfigureAwait(false);

        Exception? thrown = null;
        try
        {
            await writer.ExecuteAsync(operation, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        int after = await ReadCountAsync(rowCount, ct).ConfigureAwait(false);

        if (thrown is not null && after != before)
        {
            throw new AspireEFCoreInvariantException(
                FormattableString.Invariant(
                    $"AssertNoPartialWritesOnError: interaction threw but row count changed from {before} to {after} — partial write detected."));
        }
    }

    /// <summary>
    /// Executes the interaction twice. Uses <see cref="IDbTargetWaitForExtensions"/>
    /// to poll until the row count after the second call converges to the count after the first call
    /// within <paramref name="eventualTimeout"/>. Throws <see cref="AspireEFCoreInvariantException"/>
    /// if convergence does not occur.
    /// </summary>
    public async Task AssertIdempotentAsync(
        IInteraction operation,
        Func<DbContext, Task<int>> rowCount,
        TimeSpan eventualTimeout,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(rowCount);

        await writer.ExecuteAsync(operation, ct).ConfigureAwait(false);
        int afterFirst = await ReadCountAsync(rowCount, ct).ConfigureAwait(false);

        await writer.ExecuteAsync(operation, ct).ConfigureAwait(false);

        bool converged = false;
        try
        {
            await db.WaitForAsync(
                async ctx => await rowCount(ctx).ConfigureAwait(false) == afterFirst,
                eventualTimeout,
                ct: ct).ConfigureAwait(false);
            converged = true;
        }
        catch (TimeoutException)
        {
            converged = false;
        }

        if (!converged)
        {
            int afterSecond = await ReadCountAsync(rowCount, ct).ConfigureAwait(false);
            throw new AspireEFCoreInvariantException(
                FormattableString.Invariant(
                    $"AssertIdempotent: row count after second call ({afterSecond}) did not converge to count after first call ({afterFirst}) within {eventualTimeout.TotalSeconds:F1}s."));
        }
    }

    private async Task<int> ReadCountAsync(Func<DbContext, Task<int>> rowCount, CancellationToken ct)
    {
        DbContext ctx = db.ResolveContext(db.ResourceName);
        return await rowCount(ctx).ConfigureAwait(false);
    }
}
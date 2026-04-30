// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.EFCore;

using Microsoft.EntityFrameworkCore;

namespace Conjecture.Aspire.EFCore;

/// <summary>
/// Extension methods on <see cref="IDbTarget"/> for polling a predicate until satisfied or a timeout elapses.
/// </summary>
// Both overloads have optional parameters; suppress RS0026 as in DbStrategyExtensions.
#pragma warning disable RS0026
public static class IDbTargetWaitForExtensions
{
    private static readonly TimeSpan DefaultInitialInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan DefaultMaxInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Polls <paramref name="predicate"/> until it returns <see langword="true"/> or <paramref name="timeout"/> elapses.
    /// </summary>
    /// <param name="target">The <see cref="IDbTarget"/> to poll against.</param>
    /// <param name="predicate">Async condition to evaluate on each poll.</param>
    /// <param name="timeout">Maximum time to wait before throwing <see cref="TimeoutException"/>.</param>
    /// <param name="pollInterval">Fixed delay between polls. When <see langword="null"/> uses exponential backoff starting at 50ms, capped at 250ms.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the predicate is satisfied.</returns>
    /// <exception cref="TimeoutException">Thrown when the predicate is not satisfied within <paramref name="timeout"/>.</exception>
    public static Task WaitForAsync(
        this IDbTarget target,
        Func<DbContext, Task<bool>> predicate,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(predicate);
        return WaitForAsync(target, predicate, timeout, Task.Delay, pollInterval, ct);
    }

    /// <summary>
    /// Polls <paramref name="predicate"/> (using a typed <typeparamref name="TContext"/>) until it returns <see langword="true"/> or <paramref name="timeout"/> elapses.
    /// </summary>
    /// <typeparam name="TContext">The expected <see cref="DbContext"/> type.</typeparam>
    /// <param name="target">The <see cref="IDbTarget"/> to poll against.</param>
    /// <param name="predicate">Async condition evaluated with the resolved <typeparamref name="TContext"/> on each poll.</param>
    /// <param name="timeout">Maximum time to wait before throwing <see cref="TimeoutException"/>.</param>
    /// <param name="pollInterval">Fixed delay between polls. When <see langword="null"/> uses exponential backoff starting at 50ms, capped at 250ms.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the predicate is satisfied.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resolved context is not of type <typeparamref name="TContext"/>.</exception>
    /// <exception cref="TimeoutException">Thrown when the predicate is not satisfied within <paramref name="timeout"/>.</exception>
    public static Task WaitForAsync<TContext>(
        this IDbTarget target,
        Func<TContext, Task<bool>> predicate,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(predicate);
        return WaitForAsync<TContext>(target, predicate, timeout, Task.Delay, pollInterval, ct);
    }

#pragma warning restore RS0026

    /// <summary>
    /// Internal seam for deterministic tests — accepts a required <paramref name="delay"/> delegate replacing <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// The <paramref name="delay"/> parameter precedes <paramref name="pollInterval"/> so tests can pass it by name without specifying <paramref name="pollInterval"/>.
    /// </summary>
    internal static async Task WaitForAsync(
        this IDbTarget target,
        Func<DbContext, Task<bool>> predicate,
        TimeSpan timeout,
        Func<TimeSpan, CancellationToken, Task> delay,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        TimeSpan interval = pollInterval ?? DefaultInitialInterval;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // ResolveContext tracks the context internally (e.g. AspireDbTarget.trackedContexts) and disposes it
            // in DisposeAsync — do not wrap with await using here to avoid double-dispose.
            DbContext ctx = target.ResolveContext(target.ResourceName);
            bool satisfied = await predicate(ctx).ConfigureAwait(false);
            if (satisfied)
            {
                return;
            }

            if (sw.Elapsed >= timeout)
            {
                throw new TimeoutException(
                    FormattableString.Invariant(
                        $"WaitForAsync on '{target.ResourceName}' timed out after {sw.Elapsed.TotalMilliseconds:F0}ms elapsed."));
            }

            await delay(interval, ct).ConfigureAwait(false);

            if (pollInterval is null)
            {
                TimeSpan doubled = interval * 2;
                interval = doubled > DefaultMaxInterval ? DefaultMaxInterval : doubled;
            }
        }
    }

    /// <summary>
    /// Internal seam for deterministic tests — typed overload with required <paramref name="delay"/> delegate.
    /// </summary>
    internal static Task WaitForAsync<TContext>(
        this IDbTarget target,
        Func<TContext, Task<bool>> predicate,
        TimeSpan timeout,
        Func<TimeSpan, CancellationToken, Task> delay,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default)
        where TContext : DbContext
    {
        return WaitForAsync(
            target,
            ctx => ctx is TContext typed
                ? predicate(typed)
                : throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"Expected context of type '{typeof(TContext).Name}' but resolved '{ctx.GetType().Name}'.")),
            timeout,
            delay,
            pollInterval,
            ct);
    }
}
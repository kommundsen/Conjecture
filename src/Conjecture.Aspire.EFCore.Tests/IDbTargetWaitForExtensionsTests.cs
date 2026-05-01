// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Aspire.EFCore;
using Conjecture.EFCore;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.EFCore.Tests;

/// <summary>
/// Tests for <see cref="IDbTargetWaitForExtensions"/>.
/// Uses a minimal stub <see cref="IDbTarget"/> backed by SQLite in-memory to avoid Aspire wiring.
/// </summary>
public sealed class IDbTargetWaitForExtensionsTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;

    public async Task InitializeAsync()
    {
        connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<WaitForDbContext> opts = SharedOpts();
        await using WaitForDbContext seed = new(opts);
        await seed.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await connection.DisposeAsync();
    }

    private DbContextOptions<WaitForDbContext> SharedOpts() =>
        new DbContextOptionsBuilder<WaitForDbContext>().UseSqlite(connection).Options;

    private StubDbTarget CreateTarget(string resourceName = "test-db") =>
        new(resourceName, connection);

    // -----------------------------------------------------------------------
    // WaitForAsync — predicate immediately true
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WaitForAsync_returns_when_predicate_immediately_true()
    {
        StubDbTarget target = CreateTarget();
        List<TimeSpan> capturedDelays = [];
        Func<TimeSpan, CancellationToken, Task> fakeDelay = (ts, _) =>
        {
            capturedDelays.Add(ts);
            return Task.CompletedTask;
        };

        await target.WaitForAsync(
            static _ => Task.FromResult(true),
            timeout: TimeSpan.FromSeconds(5),
            delay: fakeDelay);

        Assert.Empty(capturedDelays);
    }

    // -----------------------------------------------------------------------
    // WaitForAsync — predicate flips after N polls
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WaitForAsync_returns_after_predicate_becomes_true()
    {
        StubDbTarget target = CreateTarget();
        int callCount = 0;
        List<TimeSpan> capturedDelays = [];
        Func<TimeSpan, CancellationToken, Task> fakeDelay = (ts, _) =>
        {
            capturedDelays.Add(ts);
            return Task.CompletedTask;
        };

        await target.WaitForAsync(
            _ =>
            {
                callCount++;
                return Task.FromResult(callCount >= 3);
            },
            timeout: TimeSpan.FromSeconds(10),
            delay: fakeDelay);

        Assert.True(callCount >= 3);
        Assert.Equal(2, capturedDelays.Count);
    }

    // -----------------------------------------------------------------------
    // WaitForAsync — throws TimeoutException with resource name and elapsed
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WaitForAsync_throws_TimeoutException_when_predicate_stays_false()
    {
        StubDbTarget target = CreateTarget("my-resource");
        Func<TimeSpan, CancellationToken, Task> fakeDelay = static (_, _) => Task.CompletedTask;

        TimeoutException ex = await Assert.ThrowsAsync<TimeoutException>(
            () => target.WaitForAsync(
                static _ => Task.FromResult(false),
                timeout: TimeSpan.FromMilliseconds(1),
                pollInterval: TimeSpan.FromMilliseconds(1),
                delay: fakeDelay));

        Assert.Contains("my-resource", ex.Message, StringComparison.Ordinal);
        // Message should also mention elapsed time — check for a number or "elapsed" keyword.
        Assert.True(
            ex.Message.Contains("elapsed", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("ms", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("second", StringComparison.OrdinalIgnoreCase),
            $"Expected elapsed time in message but got: {ex.Message}");
    }

    // -----------------------------------------------------------------------
    // WaitForAsync — cancellation propagates
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WaitForAsync_honors_cancellation_token()
    {
        StubDbTarget target = CreateTarget();
        using CancellationTokenSource cts = new();

        // Cancel immediately before calling.
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => target.WaitForAsync(
                static _ => Task.FromResult(false),
                timeout: TimeSpan.FromSeconds(30),
                ct: cts.Token));
    }

    // -----------------------------------------------------------------------
    // WaitForAsync — default backoff: first ≤ ~50ms, subsequent grow but cap ~250ms
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WaitForAsync_uses_default_backoff_when_pollInterval_is_null()
    {
        StubDbTarget target = CreateTarget();
        List<TimeSpan> capturedDelays = [];
        int callCount = 0;
        Func<TimeSpan, CancellationToken, Task> fakeDelay = (ts, _) =>
        {
            capturedDelays.Add(ts);
            return Task.CompletedTask;
        };

        // Run 5 polls (predicate false 4 times, true on the 5th).
        await target.WaitForAsync(
            _ =>
            {
                callCount++;
                return Task.FromResult(callCount >= 5);
            },
            timeout: TimeSpan.FromSeconds(30),
            delay: fakeDelay);

        // First delay ≤ ~50ms
        Assert.Equal(4, capturedDelays.Count);
        Assert.True(capturedDelays[0] <= TimeSpan.FromMilliseconds(75),
            $"First delay {capturedDelays[0]} exceeded ~50ms");

        // Each subsequent delay is >= the previous (exponential growth).
        for (int i = 1; i < capturedDelays.Count; i++)
        {
            Assert.True(capturedDelays[i] >= capturedDelays[i - 1],
                $"Delay[{i}]={capturedDelays[i]} was not >= Delay[{i - 1}]={capturedDelays[i - 1]}");
        }

        // All delays capped at ~250ms.
        foreach (TimeSpan delay in capturedDelays)
        {
            Assert.True(delay <= TimeSpan.FromMilliseconds(300),
                $"Delay {delay} exceeded cap of ~250ms");
        }
    }

    // -----------------------------------------------------------------------
    // WaitForAsync typed overload — resolves TContext
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WaitForAsync_typed_overload_resolves_TContext()
    {
        StubDbTarget target = CreateTarget();
        WaitForDbContext? resolvedCtx = null;
        Func<TimeSpan, CancellationToken, Task> fakeDelay = static (_, _) => Task.CompletedTask;

        await target.WaitForAsync<WaitForDbContext>(
            ctx =>
            {
                resolvedCtx = ctx;
                return Task.FromResult(true);
            },
            timeout: TimeSpan.FromSeconds(5),
            delay: fakeDelay);

        Assert.NotNull(resolvedCtx);
        Assert.IsType<WaitForDbContext>(resolvedCtx);
    }

    // -----------------------------------------------------------------------
    // WaitForAsync typed overload — wrong TContext throws InvalidOperationException
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WaitForAsync_typed_overload_throws_on_context_mismatch()
    {
        StubDbTarget target = CreateTarget();
        Func<TimeSpan, CancellationToken, Task> fakeDelay = static (_, _) => Task.CompletedTask;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => target.WaitForAsync<WrongDbContext>(
                static _ => Task.FromResult(true),
                timeout: TimeSpan.FromSeconds(5),
                delay: fakeDelay));
    }

    // -----------------------------------------------------------------------
    // Nested helpers and test doubles
    // -----------------------------------------------------------------------

    internal sealed class WaitForDbContext(DbContextOptions<WaitForDbContext> opts) : DbContext(opts)
    {
        public DbSet<WaitItem> Items => Set<WaitItem>();
    }

    internal sealed class WaitItem
    {
        public int Id { get; set; }
    }

    internal sealed class WrongDbContext(DbContextOptions<WrongDbContext> opts) : DbContext(opts)
    {
    }

    /// <summary>
    /// Minimal <see cref="IDbTarget"/> stub backed by a shared SQLite connection.
    /// Does not depend on <see cref="AspireDbTarget{TContext}"/> so that these tests
    /// stay focused on the extension's behavior.
    /// </summary>
    private sealed class StubDbTarget(string resourceName, SqliteConnection connection) : IDbTarget
    {
        public string ResourceName { get; } = resourceName;

        public DbContext ResolveContext(string name)
        {
            if (!string.Equals(name, ResourceName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"StubDbTarget registered as '{ResourceName}' cannot resolve '{name}'."));
            }

            DbContextOptions<WaitForDbContext> opts = new DbContextOptionsBuilder<WaitForDbContext>()
                .UseSqlite(connection)
                .Options;
            return new WaitForDbContext(opts);
        }

        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct) =>
            throw new NotSupportedException("StubDbTarget does not support ExecuteAsync.");

        public Task ResetAsync(string name, CancellationToken ct = default) =>
            throw new NotSupportedException("StubDbTarget does not support ResetAsync.");
    }
}

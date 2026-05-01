// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Conjecture.Aspire.EFCore;
using Conjecture.EFCore;

using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.EFCore.Tests;

/// <summary>Tests for <see cref="AspireDbTargetRegistry"/>.</summary>
public sealed class AspireDbTargetRegistryTests
{
    // -----------------------------------------------------------------------
    // Register — fluent chaining
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_returns_self_for_chaining()
    {
        await using AspireDbTargetRegistry registry = new();
        CountingDbTarget target = new("db-a");

        AspireDbTargetRegistry returned = registry.Register(target);

        Assert.Same(registry, returned);
    }

    // -----------------------------------------------------------------------
    // Targets — registration order preserved
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Targets_exposes_registered_targets_in_order()
    {
        await using AspireDbTargetRegistry registry = new();
        CountingDbTarget first = new("db-first");
        CountingDbTarget second = new("db-second");
        CountingDbTarget third = new("db-third");

        registry.Register(first).Register(second).Register(third);

        IReadOnlyList<IDbTarget> targets = registry.Targets;
        Assert.Equal(3, targets.Count);
        Assert.Same(first, targets[0]);
        Assert.Same(second, targets[1]);
        Assert.Same(third, targets[2]);
    }

    // -----------------------------------------------------------------------
    // ResetAllAsync — calls ResetAsync on each target sequentially
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResetAllAsync_calls_ResetAsync_on_each_target()
    {
        await using AspireDbTargetRegistry registry = new();
        SequentialCountingDbTarget first = new("db-a");
        SequentialCountingDbTarget second = new("db-b");

        registry.Register(first).Register(second);

        await registry.ResetAllAsync();

        // Both targets must have been reset exactly once.
        Assert.Equal(1, first.ResetCallCount);
        Assert.Equal(1, second.ResetCallCount);

        // Sequential: second must not have started before first completed.
        Assert.True(
            second.ResetStartedAt >= first.ResetCompletedAt,
            $"Second reset started ({second.ResetStartedAt}) before first completed ({first.ResetCompletedAt}).");
    }

    // -----------------------------------------------------------------------
    // ResetAllAsync — exception from first target stops further resets
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResetAllAsync_propagates_exceptions_from_first_failing_target()
    {
        await using AspireDbTargetRegistry registry = new();
        FailingDbTarget failing = new("db-fail");
        CountingDbTarget second = new("db-ok");

        registry.Register(failing).Register(second);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.ResetAllAsync());

        // Second target must not have been reset.
        Assert.Equal(0, second.ResetCallCount);
    }

    // -----------------------------------------------------------------------
    // DisposeAsync — disposes IAsyncDisposable targets
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_disposes_async_disposable_targets()
    {
        DisposableDbTarget first = new("db-a");
        DisposableDbTarget second = new("db-b");

        AspireDbTargetRegistry registry = new();
        registry.Register(first).Register(second);

        await registry.DisposeAsync();

        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
    }

    // -----------------------------------------------------------------------
    // Test doubles
    // -----------------------------------------------------------------------

    /// <summary>Minimal <see cref="IDbTarget"/> that counts how many times <c>ResetAsync</c> was called.</summary>
    private sealed class CountingDbTarget(string resourceName) : IDbTarget
    {
        public string ResourceName { get; } = resourceName;
        public int ResetCallCount { get; private set; }

        public DbContext ResolveContext(string name) =>
            throw new NotSupportedException("Not needed for registry tests.");

        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct) =>
            throw new NotSupportedException("Not needed for registry tests.");

        public Task ResetAsync(string name, CancellationToken ct = default)
        {
            ResetCallCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Records the wall-clock timestamps when <c>ResetAsync</c> starts and completes so tests can
    /// assert that invocations are sequential rather than concurrent.
    /// </summary>
    private sealed class SequentialCountingDbTarget(string resourceName) : IDbTarget
    {
        public string ResourceName { get; } = resourceName;
        public int ResetCallCount { get; private set; }
        public long ResetStartedAt { get; private set; }
        public long ResetCompletedAt { get; private set; }

        public DbContext ResolveContext(string name) =>
            throw new NotSupportedException("Not needed for registry tests.");

        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct) =>
            throw new NotSupportedException("Not needed for registry tests.");

        public async Task ResetAsync(string name, CancellationToken ct = default)
        {
            ResetStartedAt = Environment.TickCount64;
            await Task.Yield();
            ResetCompletedAt = Environment.TickCount64;
            ResetCallCount++;
        }
    }

    /// <summary>Always throws <see cref="InvalidOperationException"/> from <c>ResetAsync</c>.</summary>
    private sealed class FailingDbTarget(string resourceName) : IDbTarget
    {
        public string ResourceName { get; } = resourceName;

        public DbContext ResolveContext(string name) =>
            throw new NotSupportedException("Not needed for registry tests.");

        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct) =>
            throw new NotSupportedException("Not needed for registry tests.");

        public Task ResetAsync(string name, CancellationToken ct = default) =>
            throw new InvalidOperationException(FormattableString.Invariant($"Reset failed for '{name}'."));
    }

    /// <summary>Tracks whether <see cref="DisposeAsync"/> was called.</summary>
    private sealed class DisposableDbTarget(string resourceName) : IDbTarget, IAsyncDisposable
    {
        public string ResourceName { get; } = resourceName;
        public bool IsDisposed { get; private set; }

        public DbContext ResolveContext(string name) =>
            throw new NotSupportedException("Not needed for registry tests.");

        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct) =>
            throw new NotSupportedException("Not needed for registry tests.");

        public Task ResetAsync(string name, CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
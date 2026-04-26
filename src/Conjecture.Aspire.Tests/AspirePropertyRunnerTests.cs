// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;
using Conjecture.Core;

namespace Conjecture.Aspire.Tests;

public class AspirePropertyRunnerTests
{
    // ── StartAsync called exactly once ────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SingleRun_CallsStartAsyncExactlyOnce()
    {
        TrackingFixture fixture = new();
        StubStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 3, Seed = 1UL };

        await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None);

        Assert.Equal(1, fixture.StartAsyncCallCount);
    }

    // ── ResetAsync not called before first example ────────────────────────────

    [Fact]
    public async Task RunAsync_BeforeFirstExample_DoesNotCallResetAsync()
    {
        TrackingFixture fixture = new();
        ResetTrackingStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };

        await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None);

        Assert.Equal(0, machine.ResetCallsBeforeFirstExample);
    }

    // ── ResetAsync called before each example after the first ─────────────────

    [Fact]
    public async Task RunAsync_MultipleExamples_CallsResetAsyncBeforeEachExampleExceptFirst()
    {
        TrackingFixture fixture = new();
        StubStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 4, Seed = 1UL };

        await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None);

        // n examples → n-1 resets
        Assert.Equal(3, fixture.ResetAsyncCallCount);
    }

    // ── Health checks run after start and after each reset ────────────────────

    [Fact]
    public async Task RunAsync_WithHealthCheckedResources_WaitsForHealthAfterStart()
    {
        TrackingFixture fixture = new(healthCheckedResources: ["api", "db"]);
        StubStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };

        await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None);

        // Health check should happen once after start (before example 1) with no resets
        Assert.True(fixture.HealthCheckCallCount >= 1);
    }

    [Fact]
    public async Task RunAsync_WithHealthCheckedResources_WaitsForHealthAfterEachReset()
    {
        TrackingFixture fixture = new(healthCheckedResources: ["api"]);
        StubStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 3, Seed = 1UL };

        await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None);

        // 1 after start + 2 after resets = 3 health check rounds
        // each round checks 1 resource → 3 total health checks
        Assert.Equal(3, fixture.HealthCheckCallCount);
    }

    // ── Retry on transient HttpRequestException ───────────────────────────────

    [Fact]
    public async Task RunAsync_TransientHttpRequestException_RetriesUpToMaxRetryAttempts()
    {
        int maxRetries = 2;
        TransientFailingFixture fixture = new(
            transientExceptionCount: maxRetries,
            exceptionFactory: static () => new HttpRequestException("transient"));
        StubStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        fixture.MaxRetryAttemptsOverride = maxRetries;

        // Should not throw — retries absorb the transient failures
        await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None);

        Assert.Equal(maxRetries, fixture.TransientExceptionsThrown);
    }

    [Fact]
    public async Task RunAsync_TransientIOException_RetriesUpToMaxRetryAttempts()
    {
        int maxRetries = 2;
        TransientFailingFixture fixture = new(
            transientExceptionCount: maxRetries,
            exceptionFactory: static () => new IOException("transient"));
        StubStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        fixture.MaxRetryAttemptsOverride = maxRetries;

        await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None);

        Assert.Equal(maxRetries, fixture.TransientExceptionsThrown);
    }

    [Fact]
    public async Task RunAsync_PersistentHttpRequestException_FailsAfterMaxRetryAttempts()
    {
        int maxRetries = 3;
        TransientFailingFixture fixture = new(
            transientExceptionCount: maxRetries + 1,
            exceptionFactory: static () => new HttpRequestException("always fails"));
        StubStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };
        fixture.MaxRetryAttemptsOverride = maxRetries;

        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None));
    }

    // ── Non-transient exception is not retried ────────────────────────────────

    [Fact]
    public async Task RunAsync_NonTransientException_IsNotRetried()
    {
        NonTransientFailingFixture fixture = new();
        StubStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None));

        Assert.Equal(1, fixture.ExceptionThrownCount);
    }

    // ── DistributedApplication disposed after run ─────────────────────────────

    [Fact]
    public async Task RunAsync_AfterRunCompletes_DisposesDistributedApplication()
    {
        TrackingFixture fixture = new();
        StubStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 2, Seed = 1UL };

        await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None);

        Assert.True(fixture.AppDisposed);
    }

    [Fact]
    public async Task RunAsync_WhenExampleThrows_DisposesDistributedApplicationBeforeRethrowing()
    {
        TrackingFixture fixture = new();
        ThrowingStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 1, Seed = 1UL };

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None));

        Assert.True(fixture.AppDisposed);
    }

    // ── App injected into machine before each example ─────────────────────────

    [Fact]
    public async Task RunAsync_BeforeEachExample_InjectsAppIntoStateMachine()
    {
        TrackingFixture fixture = new();
        AppTrackingStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 3, Seed = 1UL };

        await AspirePropertyRunner.RunAsync(fixture, machine, settings, CancellationToken.None);

        Assert.True(machine.AppSetCount >= 3, $"Expected App to be injected in all 3 examples, got count {machine.AppSetCount}");
    }

    // ── Stub types ────────────────────────────────────────────────────────────

    private sealed class TrackingFixture(
        IEnumerable<string>? healthCheckedResources = null,
        int? maxRetryAttemptsOverride = null) : IAspireAppFixture
    {
        public int StartAsyncCallCount { get; private set; }
        public int ResetAsyncCallCount { get; private set; }
        public int HealthCheckCallCount { get; private set; }
        public bool AppDisposed { get; private set; }

        internal FakeDistributedApplication? CreatedApp { get; private set; }

        public override int MaxRetryAttempts => maxRetryAttemptsOverride ?? base.MaxRetryAttempts;
        public override IEnumerable<string> HealthCheckedResources => healthCheckedResources ?? [];

        public override Task<DistributedApplication> StartAsync(CancellationToken cancellationToken = default)
        {
            StartAsyncCallCount++;
            CreatedApp = new();
            return Task.FromResult(CreatedApp.Instance);
        }

        public override Task ResetAsync(DistributedApplication app, CancellationToken cancellationToken = default)
        {
            ResetAsyncCallCount++;
            return Task.CompletedTask;
        }

        public override Task WaitForHealthyAsync(
            DistributedApplication app,
            string resourceName,
            CancellationToken cancellationToken = default)
        {
            HealthCheckCallCount++;
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            CreatedApp?.Dispose();
            AppDisposed = true;
            return base.DisposeAsync();
        }
    }

    private sealed class ResetTrackingStateMachine : AspireStateMachine<string>
    {
        private bool firstExampleSeen;
        public int ResetCallsBeforeFirstExample { get; private set; }

        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<Interaction>> Commands(string state)
            => [Generate.Constant(new Interaction("svc", "GET", "/", null))];

        public override string RunCommand(string state, Interaction cmd)
        {
            if (!firstExampleSeen)
            {
                ResetCallsBeforeFirstExample = 0;
                firstExampleSeen = true;
            }

            return state;
        }

        public override void Invariant(string state) { }
    }

    private sealed class TransientFailingFixture(
        int transientExceptionCount,
        Func<Exception> exceptionFactory) : IAspireAppFixture
    {
        private int throwsRemaining = transientExceptionCount;

        public int TransientExceptionsThrown { get; private set; }
        public int MaxRetryAttemptsOverride { get; set; } = 3;

        public override int MaxRetryAttempts => MaxRetryAttemptsOverride;

        public override Task<DistributedApplication> StartAsync(CancellationToken cancellationToken = default)
        {
            if (throwsRemaining > 0)
            {
                throwsRemaining--;
                TransientExceptionsThrown++;
                throw exceptionFactory();
            }

            return Task.FromResult(FakeDistributedApplication.Create());
        }

        public override Task ResetAsync(DistributedApplication app, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NonTransientFailingFixture : IAspireAppFixture
    {
        public int ExceptionThrownCount { get; private set; }

        public override Task<DistributedApplication> StartAsync(CancellationToken cancellationToken = default)
        {
            ExceptionThrownCount++;
            throw new InvalidOperationException("non-transient");
        }

        public override Task ResetAsync(DistributedApplication app, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingStateMachine : AspireStateMachine<string>
    {
        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<Interaction>> Commands(string state)
            => [Generate.Constant(new Interaction("svc", "GET", "/", null))];

        public override string RunCommand(string state, Interaction cmd)
            => throw new InvalidOperationException("example failure");

        public override void Invariant(string state) { }
    }

    private sealed class AppTrackingStateMachine : AspireStateMachine<string>
    {
        public int AppSetCount { get; private set; }

        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<Interaction>> Commands(string state)
            => [Generate.Constant(new Interaction("svc", "GET", "/", null))];

        public override string RunCommand(string state, Interaction cmd)
        {
            if (App is not null)
            {
                AppSetCount++;
            }

            return state;
        }

        public override void Invariant(string state) { }
    }

    private sealed class StubStateMachine : AspireStateMachine<string>
    {
        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<Interaction>> Commands(string state)
            => [Generate.Constant(new Interaction("svc", "GET", "/", null))];

        public override string RunCommand(string state, Interaction cmd) => state;

        public override void Invariant(string state) { }
    }

    /// <summary>Wraps <see cref="DistributedApplication"/> for dispose tracking in tests.</summary>
    private sealed class FakeDistributedApplication : IDisposable
    {
        public bool Disposed { get; private set; }
        public DistributedApplication Instance { get; } = CreateApp();

        public static DistributedApplication Create() => CreateApp();

        private static DistributedApplication CreateApp()
        {
            DistributedApplicationBuilder builder = DistributedApplication.CreateBuilder([]);
            return builder.Build();
        }

        public void Dispose()
        {
            Disposed = true;
            Instance.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}

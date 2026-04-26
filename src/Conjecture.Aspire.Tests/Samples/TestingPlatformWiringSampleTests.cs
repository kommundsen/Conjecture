// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// TestingPlatform wiring: ITestSessionLifetimeHandler at session scope.
//
// In a real TestingPlatform project the fixture is started/stopped via an
// AspireSessionLifetimeHandler registered through the builder:
//
//   builder.TestHost.AddTestSessionLifetimeHandle(
//       _ => new AspireSessionLifetimeHandler(new SampleTPAspireFixture()));
//
// Inside each [Property] method, access the live app via
//   AspireSessionLifetimeHandler.Current.App
// and run the property with AspireProperty.RunAsync(handler.Fixture, machine, settings, ct).
//
// This file lives in the xUnit v2 test project and validates only the parts that
// don't require the TestingPlatform package: fixture instantiation, IAspireAppFixture
// inheritance, handler type registration, and the public runner API.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;
using Conjecture.Core;

namespace Conjecture.Aspire.Tests.Samples;

public sealed class SampleTPAspireFixture : IAspireAppFixture
{
    public override Task<DistributedApplication> StartAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateStubApp());

    public override Task ResetAsync(DistributedApplication app, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private static DistributedApplication CreateStubApp()
    {
        DistributedApplicationBuilder builder = DistributedApplication.CreateBuilder([]);
        return builder.Build();
    }
}

public sealed class TestingPlatformWiringSampleTests
{
    // ── Fixture can be instantiated and is IAspireAppFixture ──────────────────

    [Fact]
    public void TPFixture_IsIAspireAppFixture_ByInheritance()
    {
        SampleTPAspireFixture fixture = new();

        Assert.True(fixture is IAspireAppFixture);
    }

    [Fact]
    public void TPFixture_DefaultMaxRetryAttempts_IsThree()
    {
        SampleTPAspireFixture fixture = new();

        Assert.Equal(3, fixture.MaxRetryAttempts);
    }

    [Fact]
    public void TPFixture_DefaultHealthCheckedResources_IsEmpty()
    {
        SampleTPAspireFixture fixture = new();

        Assert.Empty(fixture.HealthCheckedResources);
    }

    // ── AspireSessionLifetimeHandler wraps the fixture ────────────────────────

    [Fact]
    public void AspireSessionLifetimeHandler_ExposesFixture_AsSameInstance()
    {
        SampleTPAspireFixture fixture = new();
        AspireSessionLifetimeHandler handler = new(fixture);

        Assert.Same(fixture, handler.Fixture);
    }

    [Fact]
    public void AspireSessionLifetimeHandler_IsITestSessionLifetimeHandler()
    {
        Assert.True(
            typeof(Microsoft.Testing.Platform.Extensions.TestHost.ITestSessionLifetimeHandler)
                .IsAssignableFrom(typeof(AspireSessionLifetimeHandler)));
    }

    // ── Public runner API wires session-scoped fixture into the state machine ──

    [Fact]
    public async Task AspireProperty_RunAsync_WithTPStyleFixture_CompletesWithoutThrowing()
    {
        SampleTPAspireFixture fixture = new();
        SampleTPStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 2, Seed = 1UL };

        await AspireProperty.RunAsync(fixture, machine, settings, CancellationToken.None);
    }

    // ── Stub state machine ────────────────────────────────────────────────────

    private sealed class SampleTPStateMachine : AspireStateMachine<string>
    {
        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<Interaction>> Commands(string state)
            => [Generate.Constant(new Interaction("svc", "GET", "/health", null))];

        public override string RunCommand(string state, Interaction cmd) => state;

        public override void Invariant(string state) { }
    }
}

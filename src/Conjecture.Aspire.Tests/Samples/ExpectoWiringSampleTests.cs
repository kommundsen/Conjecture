// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Expecto wiring: module-scoped testList with a `use` binding owns the
// distributed-app lifetime.
//
// In a real F# Expecto project the fixture is started/stopped like this:
//
//   module MyApp.Tests.AspireTests
//
//   open Expecto
//   open Conjecture.Aspire
//
//   let tests =
//       testList "Aspire property tests" [
//           testCase "counter increments" <| fun () ->
//               use fixture = new SampleAspireFixture()
//               let machine  = CounterStateMachine()
//               let settings = ConjectureSettings(MaxExamples = 10, Seed = 1UL)
//               AspireProperty.RunAsync(fixture, machine, settings, CancellationToken.None)
//               |> Async.AwaitTask |> Async.RunSynchronously
//       ]
//
// The `use` binding disposes the fixture when the testCase lambda returns,
// giving each test a clean application instance.
//
// This file lives in the xUnit v2 test project and validates only the parts that
// don't require the Expecto package: fixture instantiation, IAspireAppFixture
// inheritance, IAsyncDisposable disposal contract, and the public runner API.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;
using Conjecture.Core;

namespace Conjecture.Aspire.Tests.Samples;

public sealed class SampleExpectoAspireFixture : IAspireAppFixture
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

public sealed class ExpectoWiringSampleTests
{
    // ── Fixture can be instantiated and is IAspireAppFixture ──────────────────

    [Fact]
    public void ExpectoFixture_IsIAspireAppFixture_ByInheritance()
    {
        SampleExpectoAspireFixture fixture = new();

        Assert.True(fixture is not null);
    }

    [Fact]
    public void ExpectoFixture_IsIAsyncDisposable_ByInheritance()
    {
        SampleExpectoAspireFixture fixture = new();

        Assert.True(fixture is System.IAsyncDisposable);
    }

    [Fact]
    public void ExpectoFixture_DefaultMaxRetryAttempts_IsThree()
    {
        SampleExpectoAspireFixture fixture = new();

        Assert.Equal(3, fixture.MaxRetryAttempts);
    }

    [Fact]
    public void ExpectoFixture_DefaultHealthCheckedResources_IsEmpty()
    {
        SampleExpectoAspireFixture fixture = new();

        Assert.Empty(fixture.HealthCheckedResources);
    }

    // ── Fixture disposes cleanly (models the `use` binding lifetime) ──────────

    [Fact]
    public async Task ExpectoFixture_Dispose_CompletesWithoutThrowing()
    {
        SampleExpectoAspireFixture fixture = new();

        await fixture.DisposeAsync();
    }

    // ── Public runner API called from within the Expecto testCase lambda ──────

    [Fact]
    public async Task AspireProperty_RunAsync_WithExpectoStyleFixture_CompletesWithoutThrowing()
    {
        SampleExpectoAspireFixture fixture = new();
        SampleExpectoStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 2, Seed = 1UL };

        await AspireProperty.RunAsync(fixture, machine, settings, CancellationToken.None);
    }

    // ── Stub state machine ────────────────────────────────────────────────────

    private sealed class SampleExpectoStateMachine : AspireStateMachine<string>
    {
        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<Interaction>> Commands(string state)
            => [Generate.Just(new Interaction("svc", "GET", "/health", null))];

        public override string RunCommand(string state, Interaction cmd) => state;

        public override void Invariant(string state) { }
    }
}
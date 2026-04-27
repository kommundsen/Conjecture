// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// NUnit wiring: [SetUpFixture] at assembly scope holds the IAspireAppFixture.
//
// In a real NUnit project the fixture is started/stopped via:
//
//   [SetUpFixture]
//   public sealed class AspireSetUpFixture
//   {
//       public static SampleNUnitAspireFixture Fixture { get; private set; } = null!;
//
//       [OneTimeSetUp]
//       public async Task StartAsync()
//       {
//           Fixture = new SampleNUnitAspireFixture();
//           await Fixture.StartAsync();
//       }
//
//       [OneTimeTearDown]
//       public async Task StopAsync() => await Fixture.DisposeAsync();
//   }
//
// Individual test classes access the fixture via AspireSetUpFixture.Fixture and
// call AspireProperty.RunAsync(AspireSetUpFixture.Fixture, machine, settings, ct).
//
// This file lives in the xUnit v2 test project and validates only the parts that
// don't require the NUnit package: fixture instantiation, IAspireAppFixture
// inheritance, and the public runner API (AspireProperty.RunAsync).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;
using Conjecture.Core;

namespace Conjecture.Aspire.Tests.Samples;

public sealed class SampleNUnitAspireFixture : IAspireAppFixture
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

public sealed class NUnitWiringSampleTests
{
    // ── Fixture can be instantiated and is IAspireAppFixture ──────────────────

    [Fact]
    public void NUnitFixture_IsIAspireAppFixture_ByInheritance()
    {
        SampleNUnitAspireFixture fixture = new();

        Assert.True(fixture is not null);
    }

    [Fact]
    public void NUnitFixture_DefaultMaxRetryAttempts_IsThree()
    {
        SampleNUnitAspireFixture fixture = new();

        Assert.Equal(3, fixture.MaxRetryAttempts);
    }

    [Fact]
    public void NUnitFixture_DefaultHealthCheckedResources_IsEmpty()
    {
        SampleNUnitAspireFixture fixture = new();

        Assert.Empty(fixture.HealthCheckedResources);
    }

    // ── Public runner API wires assembly-scoped fixture into the state machine ─

    [Fact]
    public async Task AspireProperty_RunAsync_WithNUnitStyleFixture_CompletesWithoutThrowing()
    {
        SampleNUnitAspireFixture fixture = new();
        SampleNUnitStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 2, Seed = 1UL };

        await AspireProperty.RunAsync(fixture, machine, settings, CancellationToken.None);
    }

    // ── Stub state machine ────────────────────────────────────────────────────

    private sealed class SampleNUnitStateMachine : AspireStateMachine<string>
    {
        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<Interaction>> Commands(string state)
            => [Generate.Just(new Interaction("svc", "GET", "/health", null))];

        public override string RunCommand(string state, Interaction cmd) => state;

        public override void Invariant(string state) { }
    }
}
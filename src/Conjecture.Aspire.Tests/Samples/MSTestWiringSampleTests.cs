// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// MSTest wiring: [AssemblyInitialize] + [AssemblyCleanup] at assembly scope.
//
// In a real MSTest project the fixture is started/stopped via:
//
//   [TestClass]
//   public sealed class AspireAssemblyHooks
//   {
//       public static SampleMSTestAspireFixture Fixture { get; private set; } = null!;
//
//       [AssemblyInitialize]
//       public static async Task InitializeAsync(TestContext context)
//       {
//           Fixture = new SampleMSTestAspireFixture();
//           await Fixture.StartAsync();
//       }
//
//       [AssemblyCleanup]
//       public static async Task CleanupAsync() => await Fixture.DisposeAsync();
//   }
//
// Individual test methods access the fixture via AspireAssemblyHooks.Fixture and
// call AspireProperty.RunAsync(AspireAssemblyHooks.Fixture, machine, settings, ct).
//
// This file lives in the xUnit v2 test project and validates only the parts that
// don't require the MSTest package: fixture instantiation, IAspireAppFixture
// inheritance, and the public runner API (AspireProperty.RunAsync).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;
using Conjecture.Core;

namespace Conjecture.Aspire.Tests.Samples;

public sealed class SampleMSTestAspireFixture : IAspireAppFixture
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

public sealed class MSTestWiringSampleTests
{
    // ── Fixture can be instantiated and is IAspireAppFixture ──────────────────

    [Fact]
    public void MSTestFixture_IsIAspireAppFixture_ByInheritance()
    {
        SampleMSTestAspireFixture fixture = new();

        Assert.True(fixture is not null);
    }

    [Fact]
    public void MSTestFixture_DefaultMaxRetryAttempts_IsThree()
    {
        SampleMSTestAspireFixture fixture = new();

        Assert.Equal(3, fixture.MaxRetryAttempts);
    }

    [Fact]
    public void MSTestFixture_DefaultHealthCheckedResources_IsEmpty()
    {
        SampleMSTestAspireFixture fixture = new();

        Assert.Empty(fixture.HealthCheckedResources);
    }

    // ── Public runner API wires assembly-scoped fixture into the state machine ─

    [Fact]
    public async Task AspireProperty_RunAsync_WithMSTestStyleFixture_CompletesWithoutThrowing()
    {
        SampleMSTestAspireFixture fixture = new();
        SampleMSTestStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 2, Seed = 1UL };

        await AspireProperty.RunAsync(fixture, machine, settings, CancellationToken.None);
    }

    // ── Stub state machine ────────────────────────────────────────────────────

    private sealed class SampleMSTestStateMachine : AspireStateMachine<string>
    {
        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<Interaction>> Commands(string state)
            => [Strategy.Just(new Interaction("svc", "GET", "/health", null))];

        public override string RunCommand(string state, Interaction cmd) => state;

        public override void Invariant(string state) { }
    }
}
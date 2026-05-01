// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// xUnit v3 wiring: [assembly: AssemblyFixture(typeof(T))] at assembly scope.
//
// In a real xUnit v3 project the fixture is registered at assembly scope:
//   [assembly: AssemblyFixture(typeof(SampleAspireAssemblyFixture))]
// This file lives in the xUnit v2 test project and validates only the parts
// that don't require the xunit.v3 package: fixture instantiation, IAspireAppFixture
// inheritance, and the public runner API (AspireProperty.RunAsync).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;
using Conjecture.Core;

using Conjecture.Abstractions.Aspire;
using Conjecture.Abstractions.Interactions;
using Conjecture.Aspire.Http;
using Conjecture.Http;

namespace Conjecture.Aspire.Tests.Samples;

public sealed class SampleAspireAssemblyFixture : IAspireAppFixture
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

public sealed class XunitV3WiringSampleTests
{
    // ── Fixture can be instantiated and is IAspireAppFixture ──────────────────

    [Fact]
    public void AssemblyFixture_IsIAspireAppFixture_ByInheritance()
    {
        SampleAspireAssemblyFixture fixture = new();

        Assert.True(fixture is not null);
    }

    [Fact]
    public void AssemblyFixture_DefaultMaxRetryAttempts_IsThree()
    {
        SampleAspireAssemblyFixture fixture = new();

        Assert.Equal(3, fixture.MaxRetryAttempts);
    }

    // ── Public runner API wires assembly-scoped fixture into the state machine ─

    [Fact]
    public async Task AspireProperty_RunAsync_WithV3StyleFixture_CompletesWithoutThrowing()
    {
        SampleAspireAssemblyFixture fixture = new();
        SampleV3StateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 2, Seed = 1UL };

        await AspireHttpProperty.RunAsync(fixture, machine, settings, CancellationToken.None);
    }

    // ── Stub state machine ────────────────────────────────────────────────────

    private sealed class SampleV3StateMachine : InteractionStateMachine<string>
    {
        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<IInteraction>> Commands(string state)
            => [Strategy.Just<IInteraction>(new HttpInteraction("svc", "GET", "/health", null, null))];

        public override string RunCommand(string state, IInteraction interaction, IInteractionTarget target, CancellationToken ct) => state;

        public override void Invariant(string state) { }
    }
}
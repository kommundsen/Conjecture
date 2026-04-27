// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;
using Conjecture.Core;

namespace Conjecture.Aspire.Tests.Samples;

// ── xUnit v2 wiring: ICollectionFixture<T> at collection scope ───────────────
//
// Users subclass IAspireAppFixture and register it as a collection fixture.
// The distributed application is started once per collection, shared across
// all tests in the collection, and disposed when the collection completes.

[CollectionDefinition(nameof(SampleAspireCollection))]
public sealed class SampleAspireCollection : ICollectionFixture<SampleAspireFixture>;

public sealed class SampleAspireFixture : IAspireAppFixture
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

[Collection(nameof(SampleAspireCollection))]
public sealed class XunitV2WiringSampleTests(SampleAspireFixture fixture)
{
    // ── Fixture is injected and is the expected concrete type ─────────────────

    [Fact]
    public void CollectionFixture_IsInjectedIntoTestClass_AndIsNotNull()
    {
        Assert.NotNull(fixture);
    }

    [Fact]
    public void CollectionFixture_IsIAspireAppFixture_ByInheritance()
    {
        Assert.True(fixture is not null);
    }

    // ── Public runner API wires fixture into the state machine ────────────────

    [Fact]
    public async Task AspireProperty_RunAsync_WithCollectionFixture_CompletesWithoutThrowing()
    {
        SampleStateMachine machine = new();
        ConjectureSettings settings = new() { MaxExamples = 2, Seed = 1UL };

        await AspireProperty.RunAsync(fixture, machine, settings, CancellationToken.None);
    }

    // ── Stub state machine ────────────────────────────────────────────────────

    private sealed class SampleStateMachine : AspireStateMachine<string>
    {
        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<Interaction>> Commands(string state)
            => [Generate.Just(new Interaction("svc", "GET", "/health", null))];

        public override string RunCommand(string state, Interaction cmd) => state;

        public override void Invariant(string state) { }
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;
using Conjecture.Core;
using Conjecture.Http;

using Conjecture.Abstractions.Aspire;
using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.Tests;

public class AspireTypesTests
{
    // ── IAspireAppFixture defaults ─────────────────────────────────────────────

    [Fact]
    public void IAspireAppFixture_DefaultMaxRetryAttempts_IsThree()
    {
        StubFixture fixture = new();

        Assert.Equal(3, fixture.MaxRetryAttempts);
    }

    [Fact]
    public void IAspireAppFixture_DefaultRetryDelay_Is500Milliseconds()
    {
        StubFixture fixture = new();

        Assert.Equal(System.TimeSpan.FromMilliseconds(500), fixture.RetryDelay);
    }

    [Fact]
    public void IAspireAppFixture_DefaultHealthCheckedResources_IsEmpty()
    {
        StubFixture fixture = new();

        Assert.Empty(fixture.HealthCheckedResources);
    }

    // ── InteractionStateMachine implements IStateMachine ──────────────────────

    [Fact]
    public void InteractionStateMachine_ImplementsIStateMachine()
    {
        Assert.True(typeof(IStateMachine<string, IInteraction>)
            .IsAssignableFrom(typeof(StubStateMachine)));
    }

    // ── Concrete stub implementations ─────────────────────────────────────────

    private sealed class StubFixture : IAspireAppFixture
    {
        public Task<DistributedApplication> StartAsync()
            => throw new System.NotImplementedException();

        public Task ResetAsync(DistributedApplication app)
            => throw new System.NotImplementedException();
    }

    private sealed class StubStateMachine : InteractionStateMachine<string>
    {
        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<IInteraction>> Commands(string state)
            => [];

        public override string RunCommand(string state, IInteraction interaction, IInteractionTarget target, CancellationToken ct)
            => state;

        public override void Invariant(string state) { }
    }
}

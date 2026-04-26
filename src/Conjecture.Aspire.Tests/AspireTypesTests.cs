// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aspire.Hosting;
using Conjecture.Aspire;
using Conjecture.Core;

namespace Conjecture.Aspire.Tests;

public class AspireTypesTests
{
    // ── Interaction ────────────────────────────────────────────────────────────

    [Fact]
    public void Interaction_IsValueType()
    {
        Assert.True(typeof(Interaction).IsValueType);
    }

    [Fact]
    public void Interaction_StructuralEquality_SameValues_AreEqual()
    {
        Interaction a = new("svc", "GET", "/ping", null);
        Interaction b = new("svc", "GET", "/ping", null);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Interaction_StructuralEquality_DifferentValues_AreNotEqual()
    {
        Interaction a = new("svc", "GET", "/ping", null);
        Interaction b = new("svc", "POST", "/ping", null);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Interaction_Properties_RoundTrip()
    {
        object body = new();
        Interaction interaction = new("order-service", "PUT", "/orders/1", body);

        Assert.Equal("order-service", interaction.ResourceName);
        Assert.Equal("PUT", interaction.Method);
        Assert.Equal("/orders/1", interaction.Path);
        Assert.Same(body, interaction.Body);
    }

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

    // ── AspireStateMachine implements IStateMachine ────────────────────────────

    [Fact]
    public void AspireStateMachine_ImplementsIStateMachine()
    {
        Assert.True(typeof(IStateMachine<string, Interaction>)
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

    private sealed class StubStateMachine : AspireStateMachine<string>
    {
        public override string InitialState() => string.Empty;

        public override IEnumerable<Strategy<Interaction>> Commands(string state)
            => [];

        public override string RunCommand(string state, Interaction cmd)
            => state;

        public override void Invariant(string state) { }
    }
}

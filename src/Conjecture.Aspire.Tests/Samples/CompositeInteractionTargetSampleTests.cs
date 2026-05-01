// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Pre-Aspire multi-host sample: two plain IHosts wired into CompositeInteractionTarget.
//
// This demonstrates the multi-host story that Aspire builds on. Each host exposes
// an IInteractionTarget; CompositeInteractionTarget routes addressed interactions
// to the correct host by ResourceName.
//
// The sample under docs/samples/aspire/ shows:
//
//   CompositeInteractionTarget target = new(
//       ("order-service",   new HostInteractionTarget(orderHost)),
//       ("payment-service", new HostInteractionTarget(paymentHost)));
//
// This file validates that the CompositeInteractionTarget routing mechanics
// work as expected so the sample is self-consistent.

using System;
using System.Threading;
using System.Threading.Tasks;


using Conjecture.Interactions;
using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.Tests.Samples;

public sealed class CompositeInteractionTargetSampleTests
{
    // ── Routing: dispatches to the correct named target ───────────────────────

    [Fact]
    public async Task CompositeInteractionTarget_DispatchesToNamedTarget_ByResourceName()
    {
        StubInteractionTarget orderTarget = new("order-result");
        StubInteractionTarget paymentTarget = new("payment-result");
        CompositeInteractionTarget composite = new(
            ("order-service", orderTarget),
            ("payment-service", paymentTarget));

        object? result = await composite.ExecuteAsync(
            new StubAddressedInteraction("order-service"), CancellationToken.None);

        Assert.Equal("order-result", result);
        Assert.Equal(1, orderTarget.CallCount);
        Assert.Equal(0, paymentTarget.CallCount);
    }

    [Fact]
    public async Task CompositeInteractionTarget_DispatchesToSecondTarget_WhenResourceNameMatches()
    {
        StubInteractionTarget orderTarget = new("order-result");
        StubInteractionTarget paymentTarget = new("payment-result");
        CompositeInteractionTarget composite = new(
            ("order-service", orderTarget),
            ("payment-service", paymentTarget));

        object? result = await composite.ExecuteAsync(
            new StubAddressedInteraction("payment-service"), CancellationToken.None);

        Assert.Equal("payment-result", result);
        Assert.Equal(0, orderTarget.CallCount);
        Assert.Equal(1, paymentTarget.CallCount);
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CompositeInteractionTarget_UnknownResourceName_ThrowsInvalidOperationException()
    {
        CompositeInteractionTarget composite = new(
            ("order-service", new StubInteractionTarget("x")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            static () => new CompositeInteractionTarget(
                    ("order-service", new StubInteractionTarget("x")))
                .ExecuteAsync(new StubAddressedInteraction("unknown-service"), CancellationToken.None));
    }

    [Fact]
    public async Task CompositeInteractionTarget_NonAddressedInteraction_ThrowsInvalidOperationException()
    {
        CompositeInteractionTarget composite = new(
            ("order-service", new StubInteractionTarget("x")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => composite.ExecuteAsync(new StubNonAddressedInteraction(), CancellationToken.None));
    }

    // ── Stub implementations ──────────────────────────────────────────────────

    private sealed class StubInteractionTarget(string returnValue) : IInteractionTarget
    {
        public int CallCount { get; private set; }

        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult<object?>(returnValue);
        }
    }

    private sealed class StubAddressedInteraction(string resourceName) : IAddressedInteraction
    {
        public string ResourceName { get; } = resourceName;
    }

    private sealed class StubNonAddressedInteraction : IInteraction { }
}
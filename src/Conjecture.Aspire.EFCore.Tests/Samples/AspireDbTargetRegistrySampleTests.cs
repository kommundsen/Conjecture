// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

using Conjecture.Aspire;
using Conjecture.Aspire.EFCore;
using Conjecture.EFCore;
using Conjecture.Interactions;

using Microsoft.EntityFrameworkCore;

using Conjecture.Abstractions.Interactions;

namespace Conjecture.Aspire.EFCore.Tests.Samples;

/// <summary>
/// Demonstrates wiring <see cref="AspireDbTargetRegistry"/> inside an <see cref="IAspireAppFixture"/>
/// override so that <c>ResetAsync</c> flows through all registered <see cref="IDbTarget"/>s.
/// </summary>
public sealed class AspireDbTargetRegistrySampleTests
{
    [Fact]
    public async Task Fixture_reset_invokes_registry_reset_all()
    {
        TrackingDbTarget target = new("sample-db");

        SampleFixture fixture = new(target);
        await using (fixture)
        {
            // Simulate the framework calling ResetAsync between examples.
            await fixture.ResetAsync(app: null!, cancellationToken: default);

            Assert.Equal(1, target.ResetCallCount);
        }
    }

    // -----------------------------------------------------------------------
    // Minimal IAspireAppFixture subclass showing the registry wiring recipe
    // -----------------------------------------------------------------------

    private sealed class SampleFixture : IAspireAppFixture
    {
        private readonly AspireDbTargetRegistry registry;

        public SampleFixture(IDbTarget target)
        {
            registry = new AspireDbTargetRegistry();
            registry.Register(target);
        }

        /// <inheritdoc/>
        public override Task ResetAsync(DistributedApplication app, CancellationToken cancellationToken = default) =>
            registry.ResetAllAsync(cancellationToken);

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            await registry.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Test double
    // -----------------------------------------------------------------------

    private sealed class TrackingDbTarget(string resourceName) : IDbTarget
    {
        public string ResourceName { get; } = resourceName;
        public int ResetCallCount { get; private set; }

        public DbContext ResolveContext(string name) =>
            throw new NotSupportedException("Not needed for sample tests.");

        public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct) =>
            throw new NotSupportedException("Not needed for sample tests.");

        public Task ResetAsync(string name, CancellationToken ct = default)
        {
            ResetCallCount++;
            return Task.CompletedTask;
        }
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.TestHost;

namespace Conjecture.Aspire;

/// <summary>
/// Manages the lifetime of an <see cref="IAspireAppFixture"/> within a
/// Microsoft.Testing.Platform test session. Register via
/// <c>builder.TestHost.AddTestSessionLifetimeHandle(_ =&gt; new AspireSessionLifetimeHandler(fixture))</c>.
/// </summary>
public sealed class AspireSessionLifetimeHandler(IAspireAppFixture fixture)
    : ITestSessionLifetimeHandler
{
    /// <summary>Gets the fixture managed by this handler.</summary>
    public IAspireAppFixture Fixture { get; } = fixture;

    /// <inheritdoc />
    public string Uid => "Conjecture.Aspire.AspireSessionLifetimeHandler";

    /// <inheritdoc />
    public string Version =>
        typeof(AspireSessionLifetimeHandler).Assembly
            .GetName().Version?.ToString() ?? "1.0.0";

    /// <inheritdoc />
    public string DisplayName => "Conjecture Aspire Session Lifetime Handler";

    /// <inheritdoc />
    public string Description => "Starts and stops an Aspire distributed application fixture for a test session.";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken) =>
        Fixture.StartAsync(cancellationToken);

    /// <inheritdoc />
    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken) =>
        Fixture.DisposeAsync().AsTask();
}
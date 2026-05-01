// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Aspire.Hosting;

namespace Conjecture.Abstractions.Aspire;

/// <summary>Provides lifecycle management for an Aspire distributed application under test.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class IAspireAppFixture : IAsyncDisposable
{
    /// <summary>Maximum number of retry attempts for health-check polling. Default is 3.</summary>
    public virtual int MaxRetryAttempts => 3;

    /// <summary>Delay between retry attempts. Default is 500 milliseconds.</summary>
    public virtual TimeSpan RetryDelay => TimeSpan.FromMilliseconds(500);

    /// <summary>Resource names to health-check before each example. Empty means no health-check polling.</summary>
    public virtual IEnumerable<string> HealthCheckedResources => [];

    /// <summary>Starts the distributed application and returns the running instance.</summary>
    public virtual Task<DistributedApplication> StartAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException($"Override {nameof(StartAsync)}.");

    /// <summary>Resets application state between examples.</summary>
    public virtual Task ResetAsync(DistributedApplication app, CancellationToken cancellationToken = default)
        => throw new NotImplementedException($"Override {nameof(ResetAsync)}.");

    /// <summary>Waits until the named resource reports healthy.</summary>
    public virtual Task WaitForHealthyAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
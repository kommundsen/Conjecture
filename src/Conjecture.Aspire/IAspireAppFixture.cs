// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Aspire.Hosting;

namespace Conjecture.Aspire;

/// <summary>Provides lifecycle management for an Aspire distributed application under test.</summary>
public abstract class IAspireAppFixture : IAsyncDisposable
{
    /// <summary>Maximum number of retry attempts for health-check polling. Default is 3.</summary>
    public virtual int MaxRetryAttempts => 3;

    /// <summary>Delay between retry attempts. Default is 500 milliseconds.</summary>
    public virtual TimeSpan RetryDelay => TimeSpan.FromMilliseconds(500);

    /// <summary>Resource names to health-check before each example. Empty means no health-check polling.</summary>
    public virtual IEnumerable<string> HealthCheckedResources => [];

    /// <inheritdoc />
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
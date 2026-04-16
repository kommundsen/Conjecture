// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Conjecture.Core;

namespace Conjecture.Core.Tests;

public sealed class ConjectureObservabilityTests
{
    [Fact]
    public void ActivitySource_Name_EqualsConjectureCore()
    {
        Assert.Equal("Conjecture.Core", ConjectureObservability.ActivitySource.Name);
    }

    [Fact]
    public void Meter_Name_EqualsConjectureCore()
    {
        Assert.Equal("Conjecture.Core", ConjectureObservability.Meter.Name);
    }

    [Fact]
    public void ActivitySource_Version_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(ConjectureObservability.ActivitySource.Version));
    }

    [Fact]
    public void Meter_Version_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrEmpty(ConjectureObservability.Meter.Version));
    }

    [Fact]
    public void ActivitySource_HasListeners_DoesNotThrow()
    {
        // Verifies ActivitySource has not been disposed
        bool result = ConjectureObservability.ActivitySource.HasListeners();
        Assert.False(result); // no listeners registered in unit tests
    }
}
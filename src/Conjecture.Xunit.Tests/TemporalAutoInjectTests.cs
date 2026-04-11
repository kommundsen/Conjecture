// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Xunit;

namespace Conjecture.Xunit.Tests;

/// <summary>
/// Verifies that [Property] auto-injects temporal types via the built-in strategy resolver.
/// These tests will compile only once the temporal strategies and resolver registrations exist.
/// </summary>
public class TemporalAutoInjectTests
{
    [Property(MaxExamples = 20, Seed = 1UL)]
    public void Property_AutoInjects_DateTimeOffset(DateTimeOffset _)
    {
        Assert.True(true);
    }

    [Property(MaxExamples = 20, Seed = 1UL)]
    public void Property_AutoInjects_TimeSpan(TimeSpan _)
    {
        Assert.True(true);
    }

    [Property(MaxExamples = 20, Seed = 1UL)]
    public void Property_AutoInjects_DateOnly(DateOnly _)
    {
        Assert.True(true);
    }

    [Property(MaxExamples = 20, Seed = 1UL)]
    public void Property_AutoInjects_TimeOnly(TimeOnly _)
    {
        Assert.True(true);
    }
}
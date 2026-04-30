// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System.Collections.Concurrent;

using Conjecture.Core;
using Conjecture.Time;
using Conjecture.Xunit;

using Microsoft.Extensions.Time.Testing;

namespace Conjecture.Time.Tests;

public class TimeProviderArbitraryTests
{
    [Fact]
    public void Create_ReturnsNonNullStrategy()
    {
        Strategy<TimeProvider> strategy = TimeProviderArbitrary.Create();

        Assert.NotNull(strategy);
    }

    [Fact]
    public void Create_ReturnedStrategy_GeneratesFakeTimeProvider()
    {
        Strategy<TimeProvider> strategy = TimeProviderArbitrary.Create();

        TimeProvider generated = strategy.WithSeed(1UL).Sample();

        Assert.IsType<FakeTimeProvider>(generated);
    }

    [Property(MaxExamples = 20, Seed = 1UL)]
    public void Property_AutoInjects_TimeProvider_AsFakeTimeProvider(TimeProvider provider)
    {
        DateTimeOffset start = provider.GetUtcNow();
        FakeTimeProvider fake = Assert.IsType<FakeTimeProvider>(provider);

        fake.Advance(TimeSpan.FromSeconds(1));

        Assert.True(provider.GetUtcNow() > start);
    }

    private static readonly ConcurrentBag<FakeTimeProvider> SeenInstances = [];

    [Property(MaxExamples = 10, Seed = 2UL)]
    public void Property_EachInvocation_ReceivesFreshFakeTimeProvider(TimeProvider provider)
    {
        FakeTimeProvider fake = Assert.IsType<FakeTimeProvider>(provider);

        Assert.DoesNotContain(fake, SeenInstances);

        SeenInstances.Add(fake);
    }
}
// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Time;

using Microsoft.Extensions.Time.Testing;

namespace Conjecture.Time.Tests;

public class TimeProviderStrategyTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void ClockWithAdvances_AdvanceCountMatchesRequested(int advanceCount)
    {
        Strategy<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)> strategy =
            Strategy.ClockWithAdvances(advanceCount, TimeSpan.FromSeconds(10));
        (_, IReadOnlyList<TimeSpan> advances) = DataGen.SampleOne(strategy, seed: 1UL);

        Assert.Equal(advanceCount, advances.Count);
    }

    [Fact]
    public void ClockWithAdvances_ForwardOnly_AllAdvancesAreNonNegative()
    {
        Strategy<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)> strategy =
            Strategy.ClockWithAdvances(20, TimeSpan.FromMinutes(1), allowBackward: false);

        IReadOnlyList<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)> samples =
            DataGen.Sample(strategy, count: 10, seed: 1UL);

        Assert.All(samples, static sample =>
            Assert.All(sample.Advances, static jump =>
                Assert.True(jump >= TimeSpan.Zero, $"Expected non-negative jump, got {jump}")));
    }

    [Fact]
    public void ClockWithAdvances_WithBackward_SomeAdvancesAreNegative()
    {
        Strategy<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)> strategy =
            Strategy.ClockWithAdvances(10, TimeSpan.FromMinutes(1), allowBackward: true);

        IReadOnlyList<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)> samples =
            DataGen.Sample(strategy, count: 30, seed: 42UL);

        bool anyNegative = samples.Any(static s => s.Advances.Any(static j => j < TimeSpan.Zero));
        Assert.True(anyNegative, "Expected at least one negative advance across 30 samples with allowBackward: true");
    }

    [Fact]
    public void ClockWithAdvances_MaxJumpRespected()
    {
        TimeSpan maxJump = TimeSpan.FromSeconds(30);
        Strategy<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)> strategy =
            Strategy.ClockWithAdvances(10, maxJump, allowBackward: true);

        IReadOnlyList<(FakeTimeProvider Clock, IReadOnlyList<TimeSpan> Advances)> samples =
            DataGen.Sample(strategy, count: 20, seed: 1UL);

        Assert.All(samples, sample =>
            Assert.All(sample.Advances, jump =>
                Assert.True(jump.Duration() <= maxJump, $"Jump {jump} exceeds maxJump {maxJump}")));
    }

    [Fact]
    public void AdvancingClocks_ProducesVariety()
    {
        Strategy<FakeTimeProvider> strategy = Strategy.AdvancingClocks(TimeSpan.FromMinutes(5));

        IReadOnlyList<FakeTimeProvider> samples = DataGen.Sample(strategy, count: 20, seed: 1UL);

        System.Collections.Generic.HashSet<DateTimeOffset> distinct = [.. samples.Select(static c => c.GetUtcNow())];
        Assert.True(distinct.Count >= 3, $"Expected at least 3 distinct UtcNow starting points, got {distinct.Count}");
    }
}
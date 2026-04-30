// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core;
using Conjecture.Time;

namespace Conjecture.Time.Tests;

public class DateTimeExtensionsTests
{
    [Fact]
    public void WithKinds_ValueHasSpecifiedKind()
    {
        Strategy<(DateTime Value, DateTimeKind Kind)> strategy = Strategy.DateTimes().WithKinds();

        IReadOnlyList<(DateTime Value, DateTimeKind Kind)> samples = strategy.WithSeed(1UL).Sample(30);

        Assert.All(samples, tuple => Assert.Equal(tuple.Kind, tuple.Value.Kind));
    }

    [Fact]
    public void WithKinds_AllThreeKindsAreGenerated()
    {
        Strategy<(DateTime Value, DateTimeKind Kind)> strategy = Strategy.DateTimes().WithKinds();

        IReadOnlyList<(DateTime Value, DateTimeKind Kind)> samples = strategy.WithSeed(1UL).Sample(30);

        System.Collections.Generic.HashSet<DateTimeKind> kinds = [.. samples.Select(static t => t.Kind)];
        Assert.Contains(DateTimeKind.Utc, kinds);
        Assert.Contains(DateTimeKind.Local, kinds);
        Assert.Contains(DateTimeKind.Unspecified, kinds);
    }

    [Fact]
    public void WithKinds_ShrinksToUtcKind()
    {
        Strategy<(DateTime Value, DateTimeKind Kind)> strategy = Strategy.DateTimes().WithKinds();

        // Seed 0 produces minimum IR values; OneOf index 0 = Utc, so the minimal
        // generated tuple must carry DateTimeKind.Utc.
        (_, DateTimeKind kind) = strategy.WithSeed(0UL).Sample();

        Assert.Equal(DateTimeKind.Utc, kind);
    }

    [Fact]
    public void WithKinds_PreservesDateTimeValue()
    {
        Strategy<(DateTime Value, DateTimeKind Kind)> strategy = Strategy.DateTimes().WithKinds();

        IReadOnlyList<(DateTime Value, DateTimeKind Kind)> samples = strategy.WithSeed(2UL).Sample(30);

        Assert.All(samples, tuple =>
        {
            long expectedTicks = tuple.Value.Ticks;
            long actualTicks = DateTime.SpecifyKind(tuple.Value, DateTimeKind.Unspecified).Ticks;
            Assert.Equal(expectedTicks, actualTicks);
        });
    }
}
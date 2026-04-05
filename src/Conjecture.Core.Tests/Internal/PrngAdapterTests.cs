// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using Conjecture.Core.Internal;

namespace Conjecture.Core.Tests.Internal;

public class PrngAdapterTests
{
    [Fact]
    public void NextUInt64_ReturnsZero_WhenMaxIsZero()
    {
        var rng = new SplittableRandom(1UL);
        var result = PrngAdapter.NextUInt64(rng, 0UL);
        Assert.Equal(0UL, result);
    }

    [Fact]
    public void NextUInt64_ReturnsValueInRange()
    {
        var rng = new SplittableRandom(42UL);
        for (var i = 0; i < 1000; i++)
        {
            var value = PrngAdapter.NextUInt64(rng, 9UL);
            Assert.InRange(value, 0UL, 9UL);
        }
    }

    [Fact]
    public void NextUInt64_ReturnsValidValue_WhenMaxIsUlongMaxValue()
    {
        var rng = new SplittableRandom(7UL);
        // Should not hang or throw — any ulong is a valid result
        var value = PrngAdapter.NextUInt64(rng, ulong.MaxValue);
        Assert.InRange(value, 0UL, ulong.MaxValue);
    }

    [Fact]
    public void NextUInt64_ApproximatelyUniform()
    {
        var rng = new SplittableRandom(99UL);
        var buckets = new int[5];

        for (var i = 0; i < 10_000; i++)
        {
            buckets[PrngAdapter.NextUInt64(rng, 4UL)]++;
        }

        foreach (var count in buckets)
        {
            Assert.InRange(count, 1500, 2500);
        }
    }
}
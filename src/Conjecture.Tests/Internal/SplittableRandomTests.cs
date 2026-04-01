// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Requires InternalsVisibleTo("Conjecture.Tests") in Conjecture.Core.csproj
// to access Conjecture.Core.Internal types from this test assembly.

using Conjecture.Core.Internal;

namespace Conjecture.Tests.Internal;

public class SplittableRandomTests
{
    [Fact]
    public void SameSeed_ProducesSameBytes()
    {
        var a = new SplittableRandom(42UL);
        var b = new SplittableRandom(42UL);

        Span<byte> bufA = stackalloc byte[32];
        Span<byte> bufB = stackalloc byte[32];

        a.NextBytes(bufA);
        b.NextBytes(bufB);

        Assert.Equal(bufA.ToArray(), bufB.ToArray());
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentBytes()
    {
        var a = new SplittableRandom(1UL);
        var b = new SplittableRandom(2UL);

        Span<byte> bufA = stackalloc byte[32];
        Span<byte> bufB = stackalloc byte[32];

        a.NextBytes(bufA);
        b.NextBytes(bufB);

        Assert.False(bufA.SequenceEqual(bufB));
    }

    [Fact]
    public void NextBytes_FillsEntireBuffer()
    {
        var rng = new SplittableRandom(99UL);

        Span<byte> buf = stackalloc byte[32];
        rng.NextBytes(buf);

        // A buffer of all zeros would mean NextBytes did nothing.
        // The probability of a valid RNG producing 32 zero bytes is negligible.
        var allZero = true;
        foreach (var b in buf)
        {
            if (b != 0) { allZero = false; break; }
        }
        Assert.False(allZero, "NextBytes left the entire buffer as zeros — it likely did not write anything.");
    }

    [Fact]
    public void Split_ProducesIndependentStream()
    {
        var original = new SplittableRandom(7UL);
        IRandom split = original.Split();

        Span<byte> fromOriginal = stackalloc byte[16];
        Span<byte> fromSplit = stackalloc byte[16];

        original.NextBytes(fromOriginal);
        split.NextBytes(fromSplit);

        Assert.False(fromOriginal.SequenceEqual(fromSplit),
            "Split() produced a stream identical to its parent — it is not independent.");
    }
}
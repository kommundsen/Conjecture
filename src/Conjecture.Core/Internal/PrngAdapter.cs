// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

namespace Conjecture.Core.Internal;

internal static class PrngAdapter
{
    internal static ulong NextUInt64(IRandom rng, ulong max)
    {
        if (max == 0UL) { return 0UL; }
        if (max == ulong.MaxValue) { return rng.NextUInt64(); }

        var threshold = (ulong.MaxValue - max) % (max + 1);
        ulong x;
        do { x = rng.NextUInt64(); } while (x < threshold);
        return x % (max + 1);
    }
}
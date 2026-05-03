// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

// Derived from the Python Hypothesis library.
// Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.

using System.Numerics;

using Conjecture.Core.Internal;

namespace Conjecture.Core;

internal sealed class IntegerStrategy<T>(T min, T max) : Strategy<T> where T : IBinaryInteger<T>
{
    // True when T is a 128-bit type: T.CreateTruncating(UInt128.MaxValue) differs from
    // T.CreateTruncating(ulong.MaxValue) only for types wider than 64 bits.
    private static readonly bool Is128Bit =
        T.CreateTruncating(UInt128.MaxValue) != T.CreateTruncating(ulong.MaxValue);

    internal override T Generate(ConjectureData data)
    {
        if (min == max)
        {
            return min;
        }

        // Unsigned span of [min, max]. For signed full-range types the subtraction
        // wraps in two's-complement, yielding the correct unsigned width.
        T range = max - min;

        // Use an interleaved-around-zero encoding only when zero is strictly
        // inside [min, max] (both negative and positive values are reachable).
        // This drives the shrinker toward 0. For all-positive or all-negative
        // ranges, min + raw is used so raw = 0 maps to min (the shrinker target).
        bool useInterleaved = T.IsNegative(min) && T.IsPositive(max);

        T raw = DrawRaw(data, range);

        return useInterleaved
            ? InterleavedDecodeAroundZero(raw)
            : min + raw;
    }

    private static T DrawRaw(ConjectureData data, T range)
    {
        if (Is128Bit)
        {
            T rangeHigh = range >>> 64;
            if (rangeHigh != T.Zero)
            {
                T highMask = T.CreateTruncating(ulong.MaxValue);
                return Draw128(data, rangeHigh, range & highMask);
            }
        }

        ulong raw = data.NextInteger(0UL, ulong.CreateTruncating(range));
        return T.CreateTruncating(raw);
    }

    // Draws uniformly from [0, rangeHigh * 2^64 + rangeLow] using two IR nodes.
    private static T Draw128(ConjectureData data, T rangeHigh, T rangeLow)
    {
        ulong highUl = ulong.CreateTruncating(rangeHigh);
        ulong lowUl = ulong.CreateTruncating(rangeLow);

        ulong drawnHigh = data.NextInteger(0UL, highUl);

        ulong drawnLow = drawnHigh < highUl
            ? data.NextInteger(0UL, ulong.MaxValue)
            : data.NextInteger(0UL, lowUl);

        return (T.CreateTruncating(drawnHigh) << 64) | T.CreateTruncating(drawnLow);
    }

    private T InterleavedDecodeAroundZero(T raw)
    {
        if (raw == T.Zero)
        {
            return T.Zero;
        }

        T step = (raw + T.One) >>> 1;
        bool goNegative = T.IsOddInteger(raw);

        if (goNegative)
        {
            T candidate = T.Zero - step;
            T stepsBelow = T.Zero - min;
            return candidate >= min ? candidate : T.Zero + (step - stepsBelow);
        }

        T positiveCandidate = T.Zero + step;
        return positiveCandidate <= max ? positiveCandidate : T.Zero - (step - max);
    }
}